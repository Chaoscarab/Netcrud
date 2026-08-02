using Backend.Services;

namespace Backend.Middleware;

public class SessionAuthMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> PublicApiPaths =
    [
        "/api/auth/signup",
        "/api/auth/login"
    ];

    public async Task InvokeAsync(HttpContext context, SessionService sessionService)
    {
        var path = context.Request.Path;
        var isApiRequest = path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);
        var normalizedPath = NormalizePath(path);
        var isPublicApi = PublicApiPaths.Contains(normalizedPath);

        if (!isApiRequest || isPublicApi || HttpMethods.IsOptions(context.Request.Method))
        {
            await next(context);
            return;
        }

        if (!context.Request.Cookies.TryGetValue(SessionService.CookieName, out var cookieValue) || string.IsNullOrWhiteSpace(cookieValue))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var result = await sessionService.ValidateCookieAsync(cookieValue, context.RequestAborted);
        if (!result.IsValid)
        {
            context.Response.Cookies.Delete(SessionService.CookieName);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        context.Items["UserId"] = result.UserId;
        await next(context);
    }

    private static string NormalizePath(PathString path)
    {
        var value = path.Value?.ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return "/";
        }

        return value.Length > 1 ? value.TrimEnd('/') : value;
    }
}