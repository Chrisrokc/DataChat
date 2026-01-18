using System.Security.Cryptography;
using DataChat.Application.Common.Interfaces;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.EntityFrameworkCore;

namespace DataChat.Infrastructure.Identity;

public class AuthenticationService : IAuthenticationService
{
    private readonly IApplicationDbContext _context;

    public AuthenticationService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AuthenticationResult> AuthenticateAsync(string username, string password)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Username == username && u.IsActive);

        if (user == null)
        {
            return new AuthenticationResult(false, ErrorMessage: "Invalid username or password");
        }

        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            return new AuthenticationResult(false, ErrorMessage: "This account uses Windows Authentication");
        }

        if (!VerifyPassword(password, user.PasswordHash))
        {
            return new AuthenticationResult(false, ErrorMessage: "Invalid username or password");
        }

        // Update last login timestamp
        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();

        return new AuthenticationResult(
            true,
            user.Id,
            user.DisplayName,
            roles);
    }

    public async Task<bool> ValidatePasswordAsync(string username, string password)
    {
        var result = await AuthenticateAsync(username, password);
        return result.Success;
    }

    public string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(128 / 8);
        string hashed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: 100000,
            numBytesRequested: 256 / 8));

        return $"{Convert.ToBase64String(salt)}.{hashed}";
    }

    private bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split('.');
        if (parts.Length != 2)
            return false;

        var salt = Convert.FromBase64String(parts[0]);
        var hash = parts[1];

        string hashed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: 100000,
            numBytesRequested: 256 / 8));

        return hash == hashed;
    }
}
