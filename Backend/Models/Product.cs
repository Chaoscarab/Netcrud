using System.ComponentModel.DataAnnotations;

namespace Backend.Models;

public class Product
{
    public int Id { get; set; }

    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    // Database-computed lowercase of Name; backs the case-insensitive unique index.
    public string NameLower { get; private set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal Price { get; set; }

    // Null only for rows created before ownership existed; those are invisible to every user.
    public int? OwnerId { get; set; }

    public AppUser? Owner { get; set; }
}