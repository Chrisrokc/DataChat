using DataChat.Web.Services.Setup;

namespace DataChat.Web.Middleware;

/// <summary>
/// Middleware that redirects to the setup page when database setup is required
/// </summary>
public class SetupMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SetupMiddleware> _logger;

    // Paths that should bypass setup check
    private static readonly string[] BypassPaths = new[]
    {
        "/setup",
        "/health",
        "/_framework",
        "/_blazor",
        "/_content",
        "/css",
        "/js",
        "/lib",
        "/favicon",
        "/api/setup"
    };

    public SetupMiddleware(RequestDelegate next, ILogger<SetupMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ISetupStateService setupStateService, IConfiguration configuration)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

        // Always allow certain paths through
        if (BypassPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        // Allow static files through
        if (path.Contains('.') && !path.EndsWith(".razor"))
        {
            await _next(context);
            return;
        }

        // Check if setup is enabled
        var setupEnabled = configuration.GetValue<bool>("Setup:Enabled", true);
        if (!setupEnabled)
        {
            await _next(context);
            return;
        }

        // Check if setup is required
        try
        {
            var state = await setupStateService.GetCurrentStateAsync(context.RequestAborted);

            if (state != SetupState.Complete)
            {
                _logger.LogInformation("Setup required (state: {State}), redirecting to setup page", state);

                // Generate or retrieve setup token
                var token = setupStateService.GetSetupToken();

                // Redirect to setup with token
                context.Response.Redirect($"/setup?token={token}&state={state}");
                return;
            }
        }
        catch (Exception ex)
        {
            // If we can't determine setup state, it likely means DB is unavailable
            _logger.LogWarning(ex, "Could not determine setup state, redirecting to setup");
            var token = setupStateService.GetSetupToken();
            context.Response.Redirect($"/setup?token={token}&state={SetupState.DatabaseUnreachable}");
            return;
        }

        await _next(context);
    }
}

public static class SetupMiddlewareExtensions
{
    public static IApplicationBuilder UseSetupRequired(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SetupMiddleware>();
    }
}
