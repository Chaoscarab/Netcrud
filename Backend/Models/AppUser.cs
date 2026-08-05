using System.ComponentModel.DataAnnotations;

namespace Backend.Models;

public class AppUser
{
    public int Id { get; set; }

    [MaxLength(120)]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(120)]
    public string LastName { get; set; } = string.Empty;

    [EmailAddress]
    [MaxLength(320)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(500)]
    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<UserSession> Sessions { get; set; } = [];

    public List<Product> Products { get; set; } = [];
}