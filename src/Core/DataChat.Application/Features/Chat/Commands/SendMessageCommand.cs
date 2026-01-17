using System.Text.Json;
using DataChat.Application.Common.Interfaces;
using DataChat.Application.Features.Chat.DTOs;
using DataChat.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DataChat.Application.Features.Chat.Commands;

public record SendMessageCommand(
    Guid ChatId,
    string Content,
    IEnumerable<Guid>? DataSourceIds = null) : IRequest<ChatMessageDto>;

public class SendMessageCommandHandler : IRequestHandler<SendMessageCommand, ChatMessageDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _dateTime;
    private readonly IAiChatService _aiChatService;
    private readonly IVectorStore _vectorStore;
    private readonly IEmbeddingService _embeddingService;

    public SendMessageCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IDateTimeService dateTime,
        IAiChatService aiChatService,
        IVectorStore vectorStore,
        IEmbeddingService embeddingService)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTime = dateTime;
        _aiChatService = aiChatService;
        _vectorStore = vectorStore;
        _embeddingService = embeddingService;
    }

    public async Task<ChatMessageDto> Handle(SendMessageCommand request, CancellationToken cancellationToken)
    {
        // Verify chat ownership
        var chat = await _context.Chats
            .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(c => c.Id == request.ChatId &&
                                      c.UserId == _currentUser.UserId &&
                                      !c.IsDeleted, cancellationToken);

        if (chat == null)
            throw new UnauthorizedAccessException("Chat not found or you don't have access to it.");

        // Save user message
        var userMessage = new Domain.Entities.ChatMessage
        {
            Id = Guid.NewGuid(),
            ChatId = chat.Id,
            Role = MessageRole.User,
            Content = request.Content,
            CreatedAt = _dateTime.UtcNow
        };

        _context.ChatMessages.Add(userMessage);

        // Build conversation history for AI
        var conversationHistory = chat.Messages
            .Select(m => new ChatMessageDto(m.Id, m.Role, m.Content, m.CreatedAt))
            .ToList();
        conversationHistory.Add(new ChatMessageDto(userMessage.Id, userMessage.Role, userMessage.Content, userMessage.CreatedAt));

        // Get RAG context if data sources are specified
        string? ragContext = null;
        var usedDataSourceIds = new List<Guid>();

        if (request.DataSourceIds?.Any() == true)
        {
            // Get accessible data sources
            var accessibleSources = await _currentUser.GetAccessibleDataSourceIdsAsync();
            var allowedSources = request.DataSourceIds.Intersect(accessibleSources).ToList();

            if (allowedSources.Any())
            {
                // Generate embedding for the user query
                var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(request.Content, cancellationToken);

                // Search for relevant documents
                var searchResults = await _vectorStore.SearchAsync(
                    queryEmbedding,
                    topK: 5,
                    dataSourceFilter: allowedSources,
                    cancellationToken);

                if (searchResults.Any())
                {
                    ragContext = string.Join("\n\n---\n\n", searchResults.Select(r => r.Content));
                    usedDataSourceIds = searchResults.Select(r => r.DataSourceId).Distinct().ToList();
                }
            }
        }

        // Build system prompt
        var systemPrompt = await GetSystemPromptAsync(ragContext, cancellationToken);

        // Generate AI response
        var aiResponse = await _aiChatService.GenerateResponseAsync(
            conversationHistory,
            systemPrompt,
            cancellationToken);

        // Save assistant message
        var assistantMessage = new Domain.Entities.ChatMessage
        {
            Id = Guid.NewGuid(),
            ChatId = chat.Id,
            Role = MessageRole.Assistant,
            Content = aiResponse,
            DataSourcesUsed = usedDataSourceIds.Any() ? JsonSerializer.Serialize(usedDataSourceIds) : null,
            CreatedAt = _dateTime.UtcNow
        };

        _context.ChatMessages.Add(assistantMessage);

        // Update chat title if it's a new chat
        if (chat.Title == "New Chat" && chat.Messages.Count == 0)
        {
            chat.Title = request.Content.Length > 50
                ? request.Content[..50] + "..."
                : request.Content;
        }

        chat.UpdatedAt = _dateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return new ChatMessageDto(
            assistantMessage.Id,
            assistantMessage.Role,
            assistantMessage.Content,
            assistantMessage.CreatedAt,
            usedDataSourceIds);
    }

    private async Task<string> GetSystemPromptAsync(string? ragContext, CancellationToken cancellationToken)
    {
        var promptType = ragContext != null ? "DocumentRAG" : "DefaultChat";

        var systemPrompt = await _context.SystemPrompts
            .Where(p => p.PromptType == promptType && p.IsActive)
            .OrderByDescending(p => p.Version)
            .FirstOrDefaultAsync(cancellationToken);

        var basePrompt = systemPrompt?.Content ?? GetDefaultPrompt(promptType);

        if (ragContext != null)
        {
            return $"{basePrompt}\n\n## Relevant Context:\n{ragContext}";
        }

        return basePrompt;
    }

    private static string GetDefaultPrompt(string promptType) => promptType switch
    {
        "DocumentRAG" => """
            You are a helpful AI assistant with access to organizational documents.
            Use the provided context to answer questions accurately.
            If the context doesn't contain relevant information, say so clearly.
            Always cite which document or source your information comes from when possible.
            """,
        "DefaultChat" => """
            You are a helpful AI assistant. Provide clear, accurate, and helpful responses.
            Be concise but thorough in your explanations.
            """,
        _ => "You are a helpful AI assistant."
    };
}
