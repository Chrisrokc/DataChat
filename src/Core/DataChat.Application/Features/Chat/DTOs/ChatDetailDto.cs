namespace DataChat.Application.Features.Chat.DTOs;

public record ChatDetailDto(
    Guid Id,
    string Title,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IEnumerable<ChatMessageDto> Messages);
