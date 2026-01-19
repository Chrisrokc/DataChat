using DataChat.Domain.Entities;

namespace DataChat.Application.Common.Interfaces;

/// <summary>
/// Service for managing user's personal documents for RAG.
/// </summary>
public interface IPersonalDocumentService
{
    /// <summary>
    /// Gets or creates the personal DataSource for a user.
    /// </summary>
    Task<DataSource> GetOrCreatePersonalDataSourceAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a document to the user's personal DataSource.
    /// </summary>
    Task<Document> UploadDocumentAsync(Guid userId, Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a document from the user's personal DataSource.
    /// </summary>
    Task DeleteDocumentAsync(Guid userId, Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all documents in the user's personal DataSource.
    /// </summary>
    Task<IEnumerable<Document>> GetUserDocumentsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total storage used by a user's personal documents in bytes.
    /// </summary>
    Task<long> GetStorageUsedAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the storage limit for personal documents in bytes.
    /// </summary>
    long GetStorageLimit();

    /// <summary>
    /// Gets the maximum file size allowed for upload in bytes.
    /// </summary>
    long GetMaxFileSize();
}
