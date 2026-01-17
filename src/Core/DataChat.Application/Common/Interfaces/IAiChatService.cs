using DataChat.Application.Features.Chat.DTOs;

namespace DataChat.Application.Common.Interfaces;

public interface IAiChatService
{
    Task<string> GenerateResponseAsync(
        IEnumerable<ChatMessageDto> conversationHistory,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamResponseAsync(
        IEnumerable<ChatMessageDto> conversationHistory,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default);
}
