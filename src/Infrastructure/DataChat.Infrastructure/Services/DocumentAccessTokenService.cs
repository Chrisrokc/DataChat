using System.Text.Json;
using DataChat.Application.Common.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace DataChat.Infrastructure.Services;

/// <summary>
/// Service for generating and validating secure, time-limited document access tokens
/// using ASP.NET Core Data Protection API
/// </summary>
public class DocumentAccessTokenService : IDocumentAccessTokenService
{
    private const string Purpose = "DataChat.DocumentAccess.v1";
    private readonly ITimeLimitedDataProtector _protector;
    private readonly IApplicationDbContext _dbContext;
    private readonly ILogger<DocumentAccessTokenService> _logger;

    public DocumentAccessTokenService(
        IDataProtectionProvider dataProtectionProvider,
        IApplicationDbContext dbContext,
        ILogger<DocumentAccessTokenService> logger)
    {
        _protector = dataProtectionProvider
            .CreateProtector(Purpose)
            .ToTimeLimitedDataProtector();
        _dbContext = dbContext;
        _logger = logger;
    }

    public string GenerateToken(Guid documentId, Guid userId, Guid messageId, bool isDownload)
    {
        // Get token expiration from system configuration
        var config = _dbContext.SystemConfiguration.FirstOrDefault();
        var expirationMinutes = config?.DocumentAccessTokenExpirationMinutes ?? 10;
        var expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);

        var payload = new TokenPayload(documentId, userId, messageId, isDownload, expiresAt);
        var json = JsonSerializer.Serialize(payload);

        var token = _protector.Protect(json, TimeSpan.FromMinutes(expirationMinutes));

        // Make the token URL-safe
        return Base64UrlEncode(token);
    }

    public DocumentAccessTokenResult? ValidateToken(string token)
    {
        try
        {
            var protectedData = Base64UrlDecode(token);
            var json = _protector.Unprotect(protectedData);
            var payload = JsonSerializer.Deserialize<TokenPayload>(json);

            if (payload == null)
            {
                _logger.LogWarning("Failed to deserialize token payload");
                return null;
            }

            // Check expiration (belt and suspenders - Data Protection also checks)
            if (payload.ExpiresAt < DateTime.UtcNow)
            {
                _logger.LogWarning("Token expired for document {DocumentId}", payload.DocumentId);
                return null;
            }

            return new DocumentAccessTokenResult(
                payload.DocumentId,
                payload.UserId,
                payload.MessageId,
                payload.IsDownload,
                payload.ExpiresAt);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate document access token");
            return null;
        }
    }

    private static string Base64UrlEncode(string input)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string Base64UrlDecode(string input)
    {
        var base64 = input
            .Replace('-', '+')
            .Replace('_', '/');

        // Add padding if needed
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }

        var bytes = Convert.FromBase64String(base64);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    private record TokenPayload(
        Guid DocumentId,
        Guid UserId,
        Guid MessageId,
        bool IsDownload,
        DateTime ExpiresAt);
}
