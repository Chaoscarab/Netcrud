using System.ComponentModel.DataAnnotations;

namespace Backend.Models;

public class Product
{
    public int Id { get; set; }

    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal Price { get; set; }
}