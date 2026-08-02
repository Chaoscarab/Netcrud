using Backend.Data;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, PasswordService passwordService, SessionService sessionService) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        if (!HttpContext.Items.TryGetValue("UserId", out var userIdObj) || userIdObj is not int userId)
        {
            return NoContent();
        }

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
        {
            return NoContent();
        }

        return Ok(new UserResponse(user.Id, user.FirstName, user.LastName, user.Email));
    }

    [HttpPost("signup")]
    public async Task<ActionResult<UserResponse>> SignUp([FromBody] SignUpRequest request)
    {
        if (request.Password != request.ConfirmPassword)
        {
            return BadRequest(new { message = "Password and repeated password do not match." });
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var emailExists = await db.Users.AnyAsync(u => u.Email == email);
        if (emailExists)
        {
            return Conflict(new { message = "An account with this email already exists." });
        }

        var user = new AppUser
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            PasswordHash = passwordService.HashPassword(request.Password)
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var cookieValue = await sessionService.CreateSessionCookieValueAsync(user.Id, HttpContext.RequestAborted);
        SetSessionCookie(cookieValue);

        return Ok(new UserResponse(user.Id, user.FirstName, user.LastName, user.Email));
    }

    [HttpPost("login")]
    public async Task<ActionResult<UserResponse>> Login([FromBody] LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null || !passwordService.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid credentials." });
        }

        var cookieValue = await sessionService.CreateSessionCookieValueAsync(user.Id, HttpContext.RequestAborted);
        SetSessionCookie(cookieValue);

        return Ok(new UserResponse(user.Id, user.FirstName, user.LastName, user.Email));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        if (Request.Cookies.TryGetValue(SessionService.CookieName, out var cookieValue) && !string.IsNullOrWhiteSpace(cookieValue))
        {
            await sessionService.RevokeSessionAsync(cookieValue, HttpContext.RequestAborted);
        }

        Response.Cookies.Delete(SessionService.CookieName);
        return NoContent();
    }

    private void SetSessionCookie(string cookieValue)
    {
        Response.Cookies.Append(SessionService.CookieName, cookieValue, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(7),
            IsEssential = true
        });
    }
}