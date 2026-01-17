namespace DataChat.Application.Common.Interfaces;

public interface IAuthenticationService
{
    Task<AuthenticationResult> AuthenticateAsync(string username, string password);
    Task<bool> ValidatePasswordAsync(string username, string password);
    string HashPassword(string password);
}

public record AuthenticationResult(
    bool Success,
    Guid? UserId = null,
    string? DisplayName = null,
    IEnumerable<string>? Roles = null,
    string? ErrorMessage = null);
