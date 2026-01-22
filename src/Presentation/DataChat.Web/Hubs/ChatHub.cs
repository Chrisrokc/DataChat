using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using DataChat.Application.Common.Interfaces;
using DataChat.Application.Features.Chat.DTOs;
using DataChat.Domain.Enums;
using DataChat.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DataChat.Web.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly ICurrentUserService _currentUser;
    private readonly IAiChatService _aiChatService;
    private readonly IVectorStore _vectorStore;
    private readonly IEmbeddingService _embeddingService;
    private readonly IDocumentAccessTokenService _tokenService;
    private readonly IDateTimeService _dateTime;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        ICurrentUserService currentUser,
        IAiChatService aiChatService,
        IVectorStore vectorStore,
        IEmbeddingService embeddingService,
        IDocumentAccessTokenService tokenService,
        IDateTimeService dateTime,
        ILogger<ChatHub> logger)
    {
        _dbContextFactory = dbContextFactory;
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
        // Generate correlation ID for request tracing
        var correlationId = Guid.NewGuid().ToString("N")[..8];

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["ChatId"] = chatId,
            ["UserId"] = _currentUser.UserId ?? Guid.Empty
        });

        _logger.LogInformation("Starting message stream for chat {ChatId}", chatId);

        // Input validation (these yield directly - no try-catch needed)
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            _logger.LogWarning("Empty message received for chat {ChatId}", chatId);
            yield return "[ERROR] Message content cannot be empty.";
            yield break;
        }

        if (userMessage.Length > 32000)
        {
            _logger.LogWarning("Message too long ({Length} chars) for chat {ChatId}", userMessage.Length, chatId);
            yield return "[ERROR] Message exceeds maximum length of 32,000 characters.";
            yield break;
        }

        if (dataSourceIds?.Count > 10)
        {
            _logger.LogWarning("Too many data sources ({Count}) selected for chat {ChatId}", dataSourceIds.Count, chatId);
            yield return "[ERROR] Cannot select more than 10 data sources at once.";
            yield break;
        }

        // Capture user context before starting background task
        var userId = _currentUser.UserId;
        var accessibleSourcesTask = _currentUser.GetAccessibleDataSourceIdsAsync();
        var accessibleSources = await accessibleSourcesTask;

        // Use a channel to bridge between the exception-handling code and the yield statements
        var channel = Channel.CreateUnbounded<string>();
        var responseBuilder = new System.Text.StringBuilder();
        var usedDataSourceIds = new List<Guid>();
        var sourceChunks = new List<SourceChunkDto>();
        Guid? savedChatId = null;

        // Start the producer task that handles exceptions
        // Use a separate DbContext to avoid threading issues
        var producerTask = Task.Run(async () =>
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            try
            {
                // Verify chat ownership
                var chat = await dbContext.Chats
                    .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
                    .FirstOrDefaultAsync(c => c.Id == chatId &&
                                              c.UserId == userId &&
                                              !c.IsDeleted, cancellationToken);

                if (chat == null)
                {
                    _logger.LogWarning("Chat {ChatId} not found for user {UserId}", chatId, userId);
                    await channel.Writer.WriteAsync("[ERROR] Chat not found or you don't have access to it.", cancellationToken);
                    return;
                }

                savedChatId = chat.Id;

                // Save user message
                var userMsg = new Domain.Entities.ChatMessage
                {
                    Id = Guid.NewGuid(),
                    ChatId = chat.Id,
                    Role = MessageRole.User,
                    Content = userMessage,
                    CreatedAt = _dateTime.UtcNow
                };

                dbContext.ChatMessages.Add(userMsg);
                await dbContext.SaveChangesAsync(cancellationToken);

                // Build conversation history
                var conversationHistory = chat.Messages
                    .Select(m => new ChatMessageDto(m.Id, m.Role, m.Content, m.CreatedAt))
                    .ToList();
                conversationHistory.Add(new ChatMessageDto(userMsg.Id, userMsg.Role, userMsg.Content, userMsg.CreatedAt));

                // Get RAG context if needed
                string? systemPrompt = null;

                if (dataSourceIds?.Any() == true)
                {
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
                            var documents = await dbContext.Documents
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
                            var config = await dbContext.SystemConfiguration.Take(1).FirstOrDefaultAsync(cancellationToken);
                            var enableDocumentPreview = config?.EnableDocumentPreview ?? true;
                            var enableDocumentDownload = config?.EnableDocumentDownload ?? true;

                            // Generate a temporary message ID for token generation (will be updated after save)
                            var tempMessageId = Guid.NewGuid();

                            sourceChunks = searchResults.Select(r =>
                            {
                                var doc = documents.GetValueOrDefault(r.DocumentId);
                                var isFileSystem = doc?.DataSourceType == DataSourceType.FileSystem;

                                // Generate secure tokens only for file-based sources
                                string? viewToken = null;
                                string? downloadToken = null;

                                if (isFileSystem && userId.HasValue)
                                {
                                    if (enableDocumentPreview)
                                        viewToken = _tokenService.GenerateToken(r.DocumentId, userId.Value, tempMessageId, isDownload: false);
                                    if (enableDocumentDownload)
                                        downloadToken = _tokenService.GenerateToken(r.DocumentId, userId.Value, tempMessageId, isDownload: true);
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
                await foreach (var chunk in _aiChatService.StreamResponseAsync(
                    conversationHistory,
                    systemPrompt,
                    cancellationToken))
                {
                    responseBuilder.Append(chunk);
                    await channel.Writer.WriteAsync(chunk, cancellationToken);
                }

                _logger.LogInformation("Message stream completed for chat {ChatId}", chatId);

                // Save assistant message after streaming completes successfully
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

                dbContext.ChatMessages.Add(assistantMsg);

                // Update chat title if first message
                if (chat.Title == "New Chat" && chat.Messages.Count <= 1)
                {
                    chat.Title = userMessage.Length > 50 ? userMessage[..50] + "..." : userMessage;
                }

                chat.UpdatedAt = _dateTime.UtcNow;
                await dbContext.SaveChangesAsync(CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Message stream cancelled for chat {ChatId}", chatId);
                // Save partial response if any
                if (savedChatId.HasValue && responseBuilder.Length > 0)
                {
                    await SavePartialResponseAsync(dbContext, savedChatId.Value, responseBuilder.ToString(),
                        usedDataSourceIds, sourceChunks, userMessage);
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "AI service communication error for chat {ChatId}", chatId);
                await channel.Writer.WriteAsync($"[ERROR:{correlationId}] Unable to reach AI service. Please try again.", CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in message stream for chat {ChatId}", chatId);
                await channel.Writer.WriteAsync($"[ERROR:{correlationId}] An unexpected error occurred. Reference: {correlationId}", CancellationToken.None);
            }
            finally
            {
                channel.Writer.Complete();
            }
        }, cancellationToken);

        // Consume from the channel and yield to the client
        await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return item;
        }

        // Wait for producer to complete (should already be done when channel completes)
        await producerTask;
    }

    private async Task SavePartialResponseAsync(
        ApplicationDbContext dbContext,
        Guid chatId,
        string content,
        List<Guid> usedDataSourceIds,
        List<SourceChunkDto> sourceChunks,
        string userMessage)
    {
        try
        {
            var chat = await dbContext.Chats
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == chatId);

            if (chat == null) return;

            var assistantMsg = new Domain.Entities.ChatMessage
            {
                Id = Guid.NewGuid(),
                ChatId = chatId,
                Role = MessageRole.Assistant,
                Content = content + "\n\n*[Response interrupted]*",
                DataSourcesUsed = usedDataSourceIds.Any()
                    ? JsonSerializer.Serialize(usedDataSourceIds)
                    : null,
                SourceChunksJson = sourceChunks.Any()
                    ? JsonSerializer.Serialize(sourceChunks)
                    : null,
                CreatedAt = _dateTime.UtcNow
            };

            dbContext.ChatMessages.Add(assistantMsg);

            if (chat.Title == "New Chat" && chat.Messages.Count <= 1)
            {
                chat.Title = userMessage.Length > 50 ? userMessage[..50] + "..." : userMessage;
            }

            chat.UpdatedAt = _dateTime.UtcNow;
            await dbContext.SaveChangesAsync(CancellationToken.None);

            _logger.LogInformation("Saved partial response for cancelled chat {ChatId}", chatId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save partial response for chat {ChatId}", chatId);
        }
    }
}
