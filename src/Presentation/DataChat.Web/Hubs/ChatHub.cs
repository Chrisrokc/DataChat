using System.Runtime.CompilerServices;
using System.Text.Json;
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
    private readonly IDocumentAccessTokenService _tokenService;
    private readonly IDateTimeService _dateTime;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser,
        IAiChatService aiChatService,
        IVectorStore vectorStore,
        IEmbeddingService embeddingService,
        IDocumentAccessTokenService tokenService,
        IDateTimeService dateTime,
        ILogger<ChatHub> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _aiChatService = aiChatService;
        _vectorStore = vectorStore;
        _embeddingService = embeddingService;
        _tokenService = tokenService;
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
        var sourceChunks = new List<SourceChunkDto>();

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

                    // Get document and data source details for source preview with secure access tokens
                    var documentIds = searchResults.Select(r => r.DocumentId).Distinct().ToList();
                    var documents = await _dbContext.Documents
                        .Where(d => documentIds.Contains(d.Id))
                        .Include(d => d.DataSource)
                        .Select(d => new {
                            d.Id,
                            d.FileName,
                            d.MimeType,
                            d.DataSourceId,
                            DataSourceName = d.DataSource.Name,
                            DataSourceType = d.DataSource.Type
                        })
                        .ToDictionaryAsync(d => d.Id, cancellationToken);

                    // Get admin settings for document access features
                    var config = await _dbContext.SystemConfiguration.Take(1).FirstOrDefaultAsync(cancellationToken);
                    var enableDocumentPreview = config?.EnableDocumentPreview ?? true;
                    var enableDocumentDownload = config?.EnableDocumentDownload ?? true;

                    // Generate a temporary message ID for token generation (will be updated after save)
                    var tempMessageId = Guid.NewGuid();
                    var userId = _currentUser.UserId!.Value;

                    sourceChunks = searchResults.Select(r =>
                    {
                        var doc = documents.GetValueOrDefault(r.DocumentId);
                        var isFileSystem = doc?.DataSourceType == DataSourceType.FileSystem;

                        // Generate secure tokens only for file-based sources
                        string? viewToken = null;
                        string? downloadToken = null;

                        if (isFileSystem)
                        {
                            if (enableDocumentPreview)
                                viewToken = _tokenService.GenerateToken(r.DocumentId, userId, tempMessageId, isDownload: false);
                            if (enableDocumentDownload)
                                downloadToken = _tokenService.GenerateToken(r.DocumentId, userId, tempMessageId, isDownload: true);
                        }

                        return new SourceChunkDto(
                            ChunkId: r.DocumentChunkId,
                            DocumentId: r.DocumentId,
                            DataSourceId: r.DataSourceId,
                            DocumentName: doc?.FileName ?? "Unknown Document",
                            DataSourceName: doc?.DataSourceName ?? "Unknown Source",
                            Content: r.Content,
                            Score: r.Score,
                            ChunkIndex: null,
                            DataSourceType: doc?.DataSourceType.ToString(),
                            MimeType: doc?.MimeType,
                            ViewToken: viewToken,
                            DownloadToken: downloadToken);
                    }).ToList();

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

        // Save assistant message with source chunks for preview
        var assistantMsg = new Domain.Entities.ChatMessage
        {
            Id = Guid.NewGuid(),
            ChatId = chat.Id,
            Role = MessageRole.Assistant,
            Content = responseBuilder.ToString(),
            DataSourcesUsed = usedDataSourceIds.Any()
                ? JsonSerializer.Serialize(usedDataSourceIds)
                : null,
            SourceChunksJson = sourceChunks.Any()
                ? JsonSerializer.Serialize(sourceChunks)
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
