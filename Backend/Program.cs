using System.Net;
using System.Threading.RateLimiting;
using Backend.Data;
using Backend.Middleware;
using Backend.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
	?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

builder.Services.AddDbContext<AppDbContext>(options =>
	options.UseNpgsql(connectionString));

var dataProtection = builder.Services.AddDataProtection().SetApplicationName("netcrud");

// Without a shared key ring, every restart or extra instance invalidates all session cookies.
var keyRingPath = builder.Configuration["DataProtection:KeyRingPath"];
if (!string.IsNullOrWhiteSpace(keyRingPath))
{
	dataProtection.PersistKeysToFileSystem(Directory.CreateDirectory(keyRingPath));
}

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
	options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
	options.KnownIPNetworks.Clear();
	options.KnownProxies.Clear();

	// Only addresses listed in config are trusted to set X-Forwarded-*; otherwise clients could spoof them.
	foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
	{
		if (IPAddress.TryParse(proxy, out var address))
		{
			options.KnownProxies.Add(address);
		}
	}
});

builder.Services.AddRateLimiter(options =>
{
	options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
	options.AddPolicy(AuthController.RateLimitPolicy, context =>
		RateLimitPartition.GetFixedWindowLimiter(
			context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
			_ => new FixedWindowRateLimiterOptions
			{
				PermitLimit = 10,
				Window = TimeSpan.FromMinutes(1),
				QueueLimit = 0
			}));
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddScoped<PasswordService>();
builder.Services.AddScoped<SessionService>();
builder.Services.AddHostedService<ExpiredSessionSweeper>();

builder.Services.AddControllers();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
	app.UseHttpsRedirection();
}
else
{
	app.UseHsts();
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseAuthorization();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRateLimiter();
app.UseMiddleware<SessionAuthMiddleware>();

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();

