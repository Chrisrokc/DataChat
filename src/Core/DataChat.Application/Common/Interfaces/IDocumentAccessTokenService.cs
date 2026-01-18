namespace DataChat.Application.Common.Interfaces;

/// <summary>
/// Service for generating and validating secure, time-limited document access tokens
/// </summary>
public interface IDocumentAccessTokenService
{
    /// <summary>
    /// Generates a secure token for document access
    /// </summary>
    /// <param name="documentId">The document being accessed</param>
    /// <param name="userId">The user who is authorized to access the document</param>
    /// <param name="messageId">The chat message that referenced this document (for audit)</param>
    /// <param name="isDownload">True for download, false for in-browser preview</param>
    /// <returns>An encrypted token string</returns>
    string GenerateToken(Guid documentId, Guid userId, Guid messageId, bool isDownload);

    /// <summary>
    /// Validates and decrypts a document access token
    /// </summary>
    /// <param name="token">The encrypted token to validate</param>
    /// <returns>The token payload if valid, null if invalid or expired</returns>
    DocumentAccessTokenResult? ValidateToken(string token);
}

/// <summary>
/// Result of validating a document access token
/// </summary>
public record DocumentAccessTokenResult(
    Guid DocumentId,
    Guid UserId,
    Guid MessageId,
    bool IsDownload,
    DateTime ExpiresAt);
