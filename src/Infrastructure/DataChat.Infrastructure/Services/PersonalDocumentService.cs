using System.Security.Cryptography;
using DataChat.Application.Common.Interfaces;
using DataChat.Domain.Entities;
using DataChat.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataChat.Infrastructure.Services;

public class PersonalDocumentService : IPersonalDocumentService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IDocumentSyncService _syncService;
    private readonly ILogger<PersonalDocumentService> _logger;

    // Configuration constants
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB
    private const long StorageLimitBytes = 500 * 1024 * 1024; // 500 MB per user

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".txt", ".md",
        ".png", ".jpg", ".jpeg",
        ".xlsx", ".csv"
    };

    private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".pdf", "application/pdf" },
        { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
        { ".txt", "text/plain" },
        { ".md", "text/markdown" },
        { ".png", "image/png" },
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
        { ".csv", "text/csv" }
    };

    public PersonalDocumentService(
        IApplicationDbContext dbContext,
        IDocumentSyncService syncService,
        ILogger<PersonalDocumentService> logger)
    {
        _dbContext = dbContext;
        _syncService = syncService;
        _logger = logger;
    }

    public async Task<DataSource> GetOrCreatePersonalDataSourceAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Check if user already has a personal data source
        var existing = await _dbContext.DataSources
            .Include(d => d.FileSystemDataSource)
            .FirstOrDefaultAsync(d => d.OwnerUserId == userId, cancellationToken);

        if (existing != null)
            return existing;

        // Get user info for display name
        var user = await _dbContext.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (user == null)
            throw new InvalidOperationException($"User {userId} not found");

        // Create the uploads folder path
        var uploadsPath = Path.Combine("uploads", userId.ToString());
        var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", uploadsPath);

        // Ensure the directory exists
        Directory.CreateDirectory(fullPath);

        // Create new personal data source
        var dataSource = new DataSource
        {
            Id = Guid.NewGuid(),
            Name = "My Documents",
            Description = $"Personal documents for {user.DisplayName}",
            Type = DataSourceType.FileSystem,
            IsActive = true,
            OwnerUserId = userId,
            CreatedAt = DateTime.UtcNow,
            FileSystemDataSource = new FileSystemDataSource
            {
                FolderPath = fullPath,
                FilePatterns = "*.pdf;*.docx;*.txt;*.md;*.png;*.jpg;*.jpeg;*.xlsx;*.csv",
                IncludeSubfolders = false,
                SyncStatus = SyncStatus.Completed
            }
        };

        // Auto-grant permission to owner
        dataSource.UserPermissions.Add(new UserDataSourcePermission
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DataSourceId = dataSource.Id,
            CanRead = true,
            GrantedAt = DateTime.UtcNow,
            GrantedBy = "System"
        });

        _dbContext.DataSources.Add(dataSource);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created personal DataSource {DataSourceId} for user {UserId}", dataSource.Id, userId);

        return dataSource;
    }

    public async Task<Document> UploadDocumentAsync(Guid userId, Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        // Validate file extension
        var extension = Path.GetExtension(fileName);
        if (!AllowedExtensions.Contains(extension))
            throw new InvalidOperationException($"File type '{extension}' is not allowed. Allowed types: {string.Join(", ", AllowedExtensions)}");

        // Check file size
        if (fileStream.Length > MaxFileSizeBytes)
            throw new InvalidOperationException($"File size exceeds the maximum allowed size of {MaxFileSizeBytes / (1024 * 1024)} MB");

        // Check storage limit
        var currentUsage = await GetStorageUsedAsync(userId, cancellationToken);
        if (currentUsage + fileStream.Length > StorageLimitBytes)
            throw new InvalidOperationException($"Storage limit exceeded. Current usage: {currentUsage / (1024 * 1024)} MB, Limit: {StorageLimitBytes / (1024 * 1024)} MB");

        // Get or create personal data source
        var dataSource = await GetOrCreatePersonalDataSourceAsync(userId, cancellationToken);

        // Generate unique filename
        var sanitizedFileName = SanitizeFileName(fileName);
        var uniqueFileName = $"{Guid.NewGuid()}_{sanitizedFileName}";
        var filePath = Path.Combine(dataSource.FileSystemDataSource!.FolderPath, uniqueFileName);

        // Save file to disk
        await using (var fileOnDisk = new FileStream(filePath, FileMode.Create, FileAccess.Write))
        {
            await fileStream.CopyToAsync(fileOnDisk, cancellationToken);
        }

        // Compute file hash
        var fileHash = await ComputeFileHashAsync(filePath, cancellationToken);

        // Create document record
        var document = new Document
        {
            Id = Guid.NewGuid(),
            DataSourceId = dataSource.Id,
            FileName = fileName, // Original filename for display
            FilePath = filePath,
            FileHash = fileHash,
            FileSize = fileStream.Length,
            MimeType = MimeTypes.GetValueOrDefault(extension, contentType),
            Status = DocumentStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Documents.Add(document);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Uploaded document {DocumentId} ({FileName}) for user {UserId}", document.Id, fileName, userId);

        // Trigger document processing (sync the data source)
        try
        {
            await _syncService.StartSyncAsync(dataSource.Id, cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already running"))
        {
            // A sync is already in progress, the document will be picked up
            _logger.LogDebug("Sync already in progress for personal DataSource {DataSourceId}", dataSource.Id);
        }

        return document;
    }

    public async Task DeleteDocumentAsync(Guid userId, Guid documentId, CancellationToken cancellationToken = default)
    {
        // Get the document and verify ownership
        var document = await _dbContext.Documents
            .Include(d => d.DataSource)
            .Include(d => d.Chunks)
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

        if (document == null)
            throw new InvalidOperationException($"Document {documentId} not found");

        if (document.DataSource.OwnerUserId != userId)
            throw new UnauthorizedAccessException("You do not have permission to delete this document");

        // Delete file from disk
        if (File.Exists(document.FilePath))
        {
            try
            {
                File.Delete(document.FilePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete file from disk: {FilePath}", document.FilePath);
            }
        }

        // Delete chunks and document from database
        _dbContext.DocumentChunks.RemoveRange(document.Chunks);
        _dbContext.Documents.Remove(document);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted document {DocumentId} for user {UserId}", documentId, userId);
    }

    public async Task<IEnumerable<Document>> GetUserDocumentsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var dataSource = await _dbContext.DataSources
            .FirstOrDefaultAsync(d => d.OwnerUserId == userId, cancellationToken);

        if (dataSource == null)
            return Enumerable.Empty<Document>();

        return await _dbContext.Documents
            .Where(d => d.DataSourceId == dataSource.Id)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<long> GetStorageUsedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var dataSource = await _dbContext.DataSources
            .FirstOrDefaultAsync(d => d.OwnerUserId == userId, cancellationToken);

        if (dataSource == null)
            return 0;

        return await _dbContext.Documents
            .Where(d => d.DataSourceId == dataSource.Id)
            .SumAsync(d => d.FileSize, cancellationToken);
    }

    public long GetStorageLimit() => StorageLimitBytes;

    public long GetMaxFileSize() => MaxFileSizeBytes;

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "document" : sanitized;
    }

    private static async Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, true);
        var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
