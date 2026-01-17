using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using System.Security.Claims;

namespace DataChat.Web.Authentication;

public class ServerAuthenticationStateProvider : RevalidatingServerAuthenticationStateProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ServerAuthenticationStateProvider> _logger;

    public ServerAuthenticationStateProvider(
        ILoggerFactory loggerFactory,
        IHttpContextAccessor httpContextAccessor)
        : base(loggerFactory)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = loggerFactory.CreateLogger<ServerAuthenticationStateProvider>();
    }

    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(30);

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;

        _logger.LogInformation(
            "GetAuthenticationStateAsync called - HttpContext exists: {HasContext}, User authenticated: {IsAuth}, User name: {Name}",
            httpContext != null,
            httpContext?.User?.Identity?.IsAuthenticated ?? false,
            httpContext?.User?.Identity?.Name ?? "null");

        if (httpContext?.User?.Identity?.IsAuthenticated == true)
        {
            _logger.LogInformation("Returning authenticated state for user: {Name}", httpContext.User.Identity.Name);
            return Task.FromResult(new AuthenticationState(httpContext.User));
        }

        _logger.LogInformation("Returning anonymous authentication state");
        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    protected override Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        var isValid = authenticationState.User.Identity?.IsAuthenticated == true;
        _logger.LogInformation("ValidateAuthenticationStateAsync - IsValid: {IsValid}", isValid);
        return Task.FromResult(isValid);
    }
}
