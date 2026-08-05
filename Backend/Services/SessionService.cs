using System.Security.Cryptography;
using System.Text;
using Backend.Data;
using Backend.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class SessionService(AppDbContext db, IDataProtectionProvider dataProtectionProvider)
{
    public const string CookieName = "netcrud_session";
    private const int MaxSessionsPerUser = 5;
    private static readonly TimeSpan SessionDuration = TimeSpan.FromDays(7);
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("netcrud-session-key");

    public async Task<string> CreateSessionCookieValueAsync(int userId, CancellationToken cancellationToken = default)
    {
        var rawSessionKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var session = new UserSession
        {
            SessionId = Guid.NewGuid().ToString("N"),
            SessionKeyHash = ComputeSha256(rawSessionKey),
            ExpiresAtUtc = DateTime.UtcNow.Add(SessionDuration),
            UserId = userId
        };

        db.Sessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        await TrimSessionsAsync(userId, cancellationToken);

        return _protector.Protect(rawSessionKey);
    }

    public Task<int> DeleteExpiredSessionsAsync(CancellationToken cancellationToken = default) =>
        db.Sessions
            .Where(s => s.ExpiresAtUtc <= DateTime.UtcNow)
            .ExecuteDeleteAsync(cancellationToken);

    private async Task TrimSessionsAsync(int userId, CancellationToken cancellationToken)
    {
        var stale = await db.Sessions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.ExpiresAtUtc)
            .ThenByDescending(s => s.Id)
            .Skip(MaxSessionsPerUser)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        if (stale.Count == 0)
        {
            return;
        }

        await db.Sessions
            .Where(s => stale.Contains(s.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<SessionValidationResult> ValidateCookieAsync(string encryptedCookieValue, CancellationToken cancellationToken = default)
    {
        string rawSessionKey;
        try
        {
            rawSessionKey = _protector.Unprotect(encryptedCookieValue);
        }
        catch
        {
            return SessionValidationResult.Invalid;
        }

        var hash = ComputeSha256(rawSessionKey);
        var session = await db.Sessions
            .AsTracking()
            .FirstOrDefaultAsync(s => s.SessionKeyHash == hash, cancellationToken);

        if (session is null)
        {
            return SessionValidationResult.Invalid;
        }

        if (session.ExpiresAtUtc <= DateTime.UtcNow)
        {
            db.Sessions.Remove(session);
            await db.SaveChangesAsync(cancellationToken);
            return SessionValidationResult.Invalid;
        }

        return new SessionValidationResult(true, session.UserId);
    }

    public async Task RevokeSessionAsync(string encryptedCookieValue, CancellationToken cancellationToken = default)
    {
        string rawSessionKey;
        try
        {
            rawSessionKey = _protector.Unprotect(encryptedCookieValue);
        }
        catch
        {
            return;
        }

        var hash = ComputeSha256(rawSessionKey);
        var session = await db.Sessions.FirstOrDefaultAsync(s => s.SessionKeyHash == hash, cancellationToken);
        if (session is null)
        {
            return;
        }

        db.Sessions.Remove(session);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}

public readonly record struct SessionValidationResult(bool IsValid, int UserId)
{
    public static readonly SessionValidationResult Invalid = new(false, 0);
}