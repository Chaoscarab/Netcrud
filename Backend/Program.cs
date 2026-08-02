using Backend.Data;
using Backend.Middleware;
using Backend.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
	?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

builder.Services.AddDbContext<AppDbContext>(options =>
	options.UseNpgsql(connectionString));
builder.Services.AddDataProtection();

builder.Services.AddScoped<PasswordService>();
builder.Services.AddScoped<SessionService>();

builder.Services.AddControllers();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
	app.UseHttpsRedirection();
}

app.UseAuthorization();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseMiddleware<SessionAuthMiddleware>();

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();

