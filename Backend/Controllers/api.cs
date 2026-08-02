using Backend.Data;
using Backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProductsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Product>>> Get([FromQuery] string? filter = null)
    {
        var normalizedFilter = filter?.Trim().ToLowerInvariant();

        var query = _db.Products
            .AsNoTracking()
            .AsQueryable();

        if (normalizedFilter == "in-stock")
        {
            query = query.Where(p => p.Quantity > 0);
        }
        else if (normalizedFilter == "out-of-stock")
        {
            query = query.Where(p => p.Quantity <= 0);
        }

        var products = await query
            .OrderBy(p => p.Name)
            .ToListAsync();

        return Ok(products);
    }

    [HttpPost]
    public async Task<ActionResult<Product>> Create([FromBody] CreateProductRequest request)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { message = "Name is required." });
        }

        if (request.Quantity < 0)
        {
            return BadRequest(new { message = "Quantity cannot be negative." });
        }

        if (request.Price < 0)
        {
            return BadRequest(new { message = "Price cannot be negative." });
        }

        var nameExists = await _db.Products.AnyAsync(p => p.Name.ToLower() == name.ToLower());
        if (nameExists)
        {
            return Conflict(new { message = "Product name must be unique." });
        }

        var product = new Product
        {
            Name = name,
            Quantity = request.Quantity,
            Price = request.Price
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        return Ok(product);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (product is null)
        {
            return NotFound();
        }

        _db.Products.Remove(product);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    public sealed record CreateProductRequest(string Name, int Quantity, decimal Price);
}