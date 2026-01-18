namespace DataChat.Application.Features.Chat.DTOs;

public record ChatDto(
    Guid Id,
    string Title,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int MessageCount,
    Guid? FolderId = null,
    bool IsPinned = false);
