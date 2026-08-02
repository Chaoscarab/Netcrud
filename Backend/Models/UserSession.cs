using System.ComponentModel.DataAnnotations;

namespace Backend.Models;

public class UserSession
{
    public int Id { get; set; }

    [MaxLength(128)]
    public string SessionId { get; set; } = string.Empty;

    [MaxLength(128)]
    public string SessionKeyHash { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    public int UserId { get; set; }
    public AppUser? User { get; set; }
}