using DataChat.Domain.Enums;

namespace DataChat.Application.Features.Chat.DTOs;

public record ChatMessageDto(
    Guid Id,
    MessageRole Role,
    string Content,
    DateTime CreatedAt,
    IEnumerable<Guid>? DataSourcesUsed = null,
    IEnumerable<MessageAttachmentDto>? Attachments = null);

public record MessageAttachmentDto(
    string FileName,
    string MimeType,
    string Base64Data,
    long FileSize);
