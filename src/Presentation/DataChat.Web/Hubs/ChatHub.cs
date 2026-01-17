using System.Runtime.CompilerServices;
using DataChat.Application.Common.Interfaces;
using DataChat.Application.Features.Chat.DTOs;
using DataChat.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DataChat.Web.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IAiChatService _aiChatService;
    private readonly IVectorStore _vectorStore;
    private readonly IEmbeddingService _embeddingService;
    private readonly IDateTimeService _dateTime;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser,
        IAiChatService aiChatService,
        IVectorStore vectorStore,
        IEmbeddingService embeddingService,
        IDateTimeService dateTime,
        ILogger<ChatHub> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _aiChatService = aiChatService;
        _vectorStore = vectorStore;
        _embeddingService = embeddingService;
        _dateTime = dateTime;
        _logger = logger;
    }

    public async IAsyncEnumerable<string> StreamMessage(
        Guid chatId,
        string userMessage,
        List<Guid>? dataSourceIds,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Verify chat ownership
        var chat = await _dbContext.Chats
            .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(c => c.Id == chatId &&
                                      c.UserId == _currentUser.UserId &&
                                      !c.IsDeleted, cancellationToken);

        if (chat == null)
        {
            _logger.LogWarning("Chat {ChatId} not found for user {UserId}", chatId, _currentUser.UserId);
            yield return "[ERROR] Chat not found or you don't have access to it.";
            yield break;
        }

        // Save user message
        var userMsg = new Domain.Entities.ChatMessage
        {
            Id = Guid.NewGuid(),
            ChatId = chat.Id,
            Role = MessageRole.User,
            Content = userMessage,
            CreatedAt = _dateTime.UtcNow
        };

        _dbContext.ChatMessages.Add(userMsg);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Build conversation history
        var conversationHistory = chat.Messages
            .Select(m => new ChatMessageDto(m.Id, m.Role, m.Content, m.CreatedAt))
            .ToList();
        conversationHistory.Add(new ChatMessageDto(userMsg.Id, userMsg.Role, userMsg.Content, userMsg.CreatedAt));

        // Get RAG context if needed
        string? systemPrompt = null;
        var usedDataSourceIds = new List<Guid>();

        if (dataSourceIds?.Any() == true)
        {
            var accessibleSources = await _currentUser.GetAccessibleDataSourceIdsAsync();
            var allowedSources = dataSourceIds.Intersect(accessibleSources).ToList();

            if (allowedSources.Any())
            {
                var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(userMessage, cancellationToken);
                var searchResults = await _vectorStore.SearchAsync(queryEmbedding, topK: 5, allowedSources, cancellationToken);

                if (searchResults.Any())
                {
                    var ragContext = string.Join("\n\n---\n\n", searchResults.Select(r => r.Content));
                    usedDataSourceIds = searchResults.Select(r => r.DataSourceId).Distinct().ToList();

                    systemPrompt = $"""
                        You are a helpful AI assistant with access to organizational documents.
                        Use the provided context to answer questions accurately.
                        If the context doesn't contain relevant information, say so clearly.

                        ## Relevant Context:
                        {ragContext}
                        """;
                }
            }
        }

        // Stream the AI response
        var responseBuilder = new System.Text.StringBuilder();

        await foreach (var chunk in _aiChatService.StreamResponseAsync(
            conversationHistory,
            systemPrompt,
            cancellationToken))
        {
            responseBuilder.Append(chunk);
            yield return chunk;
        }

        // Save assistant message
        var assistantMsg = new Domain.Entities.ChatMessage
        {
            Id = Guid.NewGuid(),
            ChatId = chat.Id,
            Role = MessageRole.Assistant,
            Content = responseBuilder.ToString(),
            DataSourcesUsed = usedDataSourceIds.Any()
                ? System.Text.Json.JsonSerializer.Serialize(usedDataSourceIds)
                : null,
            CreatedAt = _dateTime.UtcNow
        };

        _dbContext.ChatMessages.Add(assistantMsg);

        // Update chat title if first message
        if (chat.Title == "New Chat" && chat.Messages.Count <= 1)
        {
            chat.Title = userMessage.Length > 50 ? userMessage[..50] + "..." : userMessage;
        }

        chat.UpdatedAt = _dateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
