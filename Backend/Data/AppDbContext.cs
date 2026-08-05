using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<UserSession> Sessions => Set<UserSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Product>()
            .Property(p => p.NameLower)
            .HasMaxLength(120)
            .HasComputedColumnSql("lower(\"Name\")", stored: true);

        // Uniqueness is per owner and case-insensitive, matching the controller's duplicate check.
        modelBuilder.Entity<Product>()
            .HasIndex(p => new { p.OwnerId, p.NameLower })
            .IsUnique();

        modelBuilder.Entity<Product>()
            .HasOne(p => p.Owner)
            .WithMany(u => u.Products)
            .HasForeignKey(p => p.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserSession>()
            .HasIndex(s => s.SessionKeyHash)
            .IsUnique();

        modelBuilder.Entity<UserSession>()
            .HasIndex(s => s.ExpiresAtUtc);

        modelBuilder.Entity<UserSession>()
            .HasOne(s => s.User)
            .WithMany(u => u.Sessions)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}