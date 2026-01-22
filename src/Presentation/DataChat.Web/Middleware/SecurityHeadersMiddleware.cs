namespace DataChat.Web.Middleware;

/// <summary>
/// Middleware that adds security headers to all HTTP responses.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // Prevent MIME type sniffing
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");

        // Clickjacking protection
        context.Response.Headers.Append("X-Frame-Options", "DENY");

        // XSS protection (legacy but still useful for older browsers)
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");

        // Referrer policy
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

        // Permissions policy - disable unnecessary browser features
        context.Response.Headers.Append("Permissions-Policy",
            "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()");

        // Content Security Policy
        // Note: 'unsafe-inline' and 'unsafe-eval' required for Blazor Server
        context.Response.Headers.Append("Content-Security-Policy",
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data: blob:; " +
            "connect-src 'self' wss: ws:; " +
            "frame-ancestors 'none';");

        await _next(context);
    }
}

public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
        => app.UseMiddleware<SecurityHeadersMiddleware>();
}
