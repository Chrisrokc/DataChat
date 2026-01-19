using System.ClientModel;
using System.Runtime.CompilerServices;
using System.Text;
using Azure;
using Azure.AI.OpenAI;
using DataChat.Application.Common.Interfaces;
using DataChat.Application.Features.Chat.DTOs;
using DataChat.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using PDFtoImage;
using SkiaSharp;

namespace DataChat.Infrastructure.AI.AzureOpenAI;

public class AzureOpenAiChatService : IAiChatService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ISecureConfigurationService _secureConfig;
    private readonly ILogger<AzureOpenAiChatService> _logger;

    public AzureOpenAiChatService(
        IApplicationDbContext dbContext,
        ISecureConfigurationService secureConfig,
        ILogger<AzureOpenAiChatService> logger)
    {
        _dbContext = dbContext;
        _secureConfig = secureConfig;
        _logger = logger;
    }

    public async Task<string> GenerateResponseAsync(
        IEnumerable<ChatMessageDto> conversationHistory,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        var (client, options) = await GetClientAndOptionsAsync(cancellationToken);

        var messages = BuildMessages(conversationHistory, systemPrompt);

        var completion = await client.CompleteChatAsync(messages, options, cancellationToken);

        return completion.Value.Content[0].Text;
    }

    public async IAsyncEnumerable<string> StreamResponseAsync(
        IEnumerable<ChatMessageDto> conversationHistory,
        string? systemPrompt = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var (client, options) = await GetClientAndOptionsAsync(cancellationToken);

        var messages = BuildMessages(conversationHistory, systemPrompt);

        await foreach (var update in client.CompleteChatStreamingAsync(messages, options, cancellationToken))
        {
            foreach (var contentPart in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(contentPart.Text))
                {
                    yield return contentPart.Text;
                }
            }
        }
    }

    private async Task<(ChatClient client, ChatCompletionOptions options)> GetClientAndOptionsAsync(
        CancellationToken cancellationToken)
    {
        var config = await _dbContext.SystemConfiguration
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("System configuration not found");

        if (string.IsNullOrEmpty(config.AzureOpenAiEndpoint))
            throw new InvalidOperationException("Azure OpenAI endpoint is not configured");

        if (string.IsNullOrEmpty(config.AzureOpenAiApiKey))
            throw new InvalidOperationException("Azure OpenAI API key is not configured");

        if (string.IsNullOrEmpty(config.AzureOpenAiDeploymentName))
            throw new InvalidOperationException("Azure OpenAI deployment name is not configured");

        var apiKey = _secureConfig.Decrypt(config.AzureOpenAiApiKey);
        var endpoint = new Uri(config.AzureOpenAiEndpoint);

        var azureClient = new AzureOpenAIClient(endpoint, new AzureKeyCredential(apiKey));
        var chatClient = azureClient.GetChatClient(config.AzureOpenAiDeploymentName);

        // Note: Newer models (gpt-4o, gpt-5.x, etc.) have restrictions:
        // - They require 'max_completion_tokens' instead of 'max_tokens'
        // - Some models only support temperature=1
        // We use default options to maximize compatibility with all Azure models.
        var options = new ChatCompletionOptions();

        return (chatClient, options);
    }

    private static List<ChatMessage> BuildMessages(
        IEnumerable<ChatMessageDto> conversationHistory,
        string? systemPrompt)
    {
        var messages = new List<ChatMessage>();

        if (!string.IsNullOrEmpty(systemPrompt))
        {
            messages.Add(new SystemChatMessage(systemPrompt));
        }

        foreach (var msg in conversationHistory)
        {
            messages.Add(msg.Role switch
            {
                MessageRole.User => BuildUserMessage(msg),
                MessageRole.Assistant => new AssistantChatMessage(msg.Content),
                MessageRole.System => new SystemChatMessage(msg.Content),
                _ => throw new ArgumentException($"Unknown message role: {msg.Role}")
            });
        }

        return messages;
    }

    private static ChatMessage BuildUserMessage(ChatMessageDto dto)
    {
        // If no attachments, use simple text message
        if (dto.Attachments?.Any() != true)
        {
            return new UserChatMessage(dto.Content);
        }

        // Build multi-modal content with text, images, and document content
        var contentParts = new List<ChatMessageContentPart>();

        // Add user's text content first (if any)
        if (!string.IsNullOrEmpty(dto.Content))
        {
            contentParts.Add(ChatMessageContentPart.CreateTextPart(dto.Content));
        }

        // Add image attachments as images
        foreach (var attachment in dto.Attachments.Where(a => IsImageMimeType(a.MimeType)))
        {
            var imageBytes = Convert.FromBase64String(attachment.Base64Data);
            var binaryData = BinaryData.FromBytes(imageBytes);
            contentParts.Add(ChatMessageContentPart.CreateImagePart(binaryData, attachment.MimeType));
        }

        // Handle PDF attachments - render as images for vision API
        foreach (var attachment in dto.Attachments.Where(a => a.MimeType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)))
        {
            var pdfBytes = Convert.FromBase64String(attachment.Base64Data);
            var pageImages = RenderPdfPagesToImages(pdfBytes);
            contentParts.Add(ChatMessageContentPart.CreateTextPart($"\n\n--- PDF: {attachment.FileName} ({pageImages.Count} page(s)) ---\n"));
            foreach (var pageImage in pageImages)
            {
                var binaryData = BinaryData.FromBytes(pageImage);
                contentParts.Add(ChatMessageContentPart.CreateImagePart(binaryData, "image/png"));
            }
        }

        // Add other document attachments as extracted text
        foreach (var attachment in dto.Attachments.Where(a => IsDocumentMimeType(a.MimeType) && !a.MimeType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)))
        {
            var documentText = ExtractDocumentText(attachment);
            if (!string.IsNullOrWhiteSpace(documentText))
            {
                var documentContent = $"\n\n--- Content from attached file: {attachment.FileName} ---\n{documentText}\n--- End of {attachment.FileName} ---\n";
                contentParts.Add(ChatMessageContentPart.CreateTextPart(documentContent));
            }
        }

        return new UserChatMessage(contentParts);
    }

    private static bool IsImageMimeType(string mimeType) =>
        mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    private static bool IsDocumentMimeType(string mimeType) =>
        mimeType.ToLowerInvariant() switch
        {
            "application/pdf" => true,
            "text/plain" => true,
            "text/csv" => true,
            "text/markdown" => true,
            "application/json" => true,
            _ => false
        };

    private static string ExtractDocumentText(MessageAttachmentDto attachment)
    {
        try
        {
            var bytes = Convert.FromBase64String(attachment.Base64Data);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return $"[Unable to extract text from {attachment.FileName}]";
        }
    }

    private static List<byte[]> RenderPdfPagesToImages(byte[] pdfBytes)
    {
        var images = new List<byte[]>();
        var pageCount = Conversion.GetPageCount(pdfBytes);

        for (int i = 0; i < pageCount; i++)
        {
            using var bitmap = Conversion.ToImage(pdfBytes, page: i);
            using var data = bitmap.Encode(SKEncodedImageFormat.Png, 90);
            images.Add(data.ToArray());
        }

        return images;
    }
}
