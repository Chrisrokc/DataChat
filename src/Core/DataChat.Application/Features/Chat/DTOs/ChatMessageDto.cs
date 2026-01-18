using DataChat.Domain.Enums;

namespace DataChat.Application.Features.Chat.DTOs;

public record ChatMessageDto(
    Guid Id,
    MessageRole Role,
    string Content,
    DateTime CreatedAt,
    IEnumerable<Guid>? DataSourcesUsed = null,
    IEnumerable<MessageAttachmentDto>? Attachments = null,
    IEnumerable<SourceChunkDto>? SourceChunks = null);

public record MessageAttachmentDto(
    string FileName,
    string MimeType,
    string Base64Data,
    long FileSize);

/// <summary>
/// Represents a document chunk used as a source in RAG responses
/// </summary>
public record SourceChunkDto(
    Guid ChunkId,
    Guid DocumentId,
    Guid DataSourceId,
    string DocumentName,
    string DataSourceName,
    string Content,
    float Score,
    int? ChunkIndex = null,
    string? DataSourceType = null,    // "FileSystem" or "SqlView"
    string? MimeType = null,          // For file type icons
    string? ViewToken = null,         // Secure view URL token
    string? DownloadToken = null);    // Secure download URL token
