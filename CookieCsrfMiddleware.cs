using System.Security.Cryptography;
using System.Text;

namespace XeGhepApp.Data;

public sealed class CookieCsrfMiddleware
{
    private readonly RequestDelegate _next;
    public CookieCsrfMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var unsafeMethod = context.Request.Method is not ("GET" or "HEAD" or "OPTIONS" or "TRACE");
        var cookieAuth = context.Request.Cookies.ContainsKey("xeghep_access")
            && !context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
        var ajaxEndpoint = context.Request.Path.StartsWithSegments("/api")
            || context.Request.Path.Value?.EndsWith(".php", StringComparison.OrdinalIgnoreCase) == true;
        if (unsafeMethod && cookieAuth && ajaxEndpoint)
        {
            var cookie = context.Request.Cookies["XSRF-TOKEN"];
            var header = context.Request.Headers["X-XSRF-TOKEN"].ToString();
            if (string.IsNullOrEmpty(cookie) || string.IsNullOrEmpty(header)
                || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(cookie), Encoding.UTF8.GetBytes(header)))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { message = "Yêu cầu CSRF không hợp lệ." });
                return;
            }
        }
        await _next(context);
    }
}
