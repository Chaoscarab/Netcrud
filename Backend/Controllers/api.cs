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
    public async Task<ActionResult<IEnumerable<ProductResponse>>> Get([FromQuery] string? filter = null)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var normalizedFilter = filter?.Trim().ToLowerInvariant();

        var query = _db.Products
            .AsNoTracking()
            .Where(p => p.OwnerId == userId);

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
            .Select(p => new ProductResponse(p.Id, p.Name, p.Quantity, p.Price))
            .ToListAsync(HttpContext.RequestAborted);

        return Ok(products);
    }

    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create([FromBody] CreateProductRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

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

        var lowered = name.ToLowerInvariant();
        var nameExists = await _db.Products
            .AnyAsync(p => p.OwnerId == userId && p.NameLower == lowered, HttpContext.RequestAborted);
        if (nameExists)
        {
            return Conflict(new { message = "Product name must be unique." });
        }

        var product = new Product
        {
            Name = name,
            Quantity = request.Quantity,
            Price = request.Price,
            OwnerId = userId
        };

        _db.Products.Add(product);

        try
        {
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return Conflict(new { message = "Product name must be unique." });
        }

        return Ok(new ProductResponse(product.Id, product.Name, product.Quantity, product.Price));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == userId, HttpContext.RequestAborted);
        if (product is null)
        {
            // Same response whether the row is missing or owned by someone else.
            return NotFound();
        }

        _db.Products.Remove(product);
        await _db.SaveChangesAsync(HttpContext.RequestAborted);

        return NoContent();
    }

    private bool TryGetUserId(out int userId)
    {
        if (HttpContext.Items.TryGetValue("UserId", out var value) && value is int id)
        {
            userId = id;
            return true;
        }

        userId = 0;
        return false;
    }

    public sealed record CreateProductRequest(string Name, int Quantity, decimal Price);

    public sealed record ProductResponse(int Id, string Name, int Quantity, decimal Price);
}