using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DataChat.Application.Common.Interfaces;
using DataChat.Application.Features.Chat.DTOs;
using DataChat.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataChat.Infrastructure.AI.Ollama;

public class OllamaChatService : IAiChatService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OllamaChatService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public OllamaChatService(
        IApplicationDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        ILogger<OllamaChatService> logger)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string> GenerateResponseAsync(
        IEnumerable<ChatMessageDto> conversationHistory,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        var (endpoint, model) = await GetConfigAsync(cancellationToken);
        var client = _httpClientFactory.CreateClient("Ollama");
        client.BaseAddress = new Uri(endpoint);

        var messages = BuildMessages(conversationHistory, systemPrompt);
        var request = new OllamaChatRequest
        {
            Model = model,
            Messages = messages,
            Stream = false
        };

        var response = await client.PostAsJsonAsync("/api/chat", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(JsonOptions, cancellationToken);
        return result?.Message?.Content ?? string.Empty;
    }

    public async IAsyncEnumerable<string> StreamResponseAsync(
        IEnumerable<ChatMessageDto> conversationHistory,
        string? systemPrompt = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var (endpoint, model) = await GetConfigAsync(cancellationToken);
        var client = _httpClientFactory.CreateClient("Ollama");
        client.BaseAddress = new Uri(endpoint);

        var messages = BuildMessages(conversationHistory, systemPrompt);
        var request = new OllamaChatRequest
        {
            Model = model,
            Messages = messages,
            Stream = true
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = new StringContent(JsonSerializer.Serialize(request, JsonOptions), Encoding.UTF8, "application/json")
        };

        var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line)) continue;

            var chunk = JsonSerializer.Deserialize<OllamaChatResponse>(line, JsonOptions);
            if (chunk?.Message?.Content != null)
            {
                yield return chunk.Message.Content;
            }

            if (chunk?.Done == true)
            {
                break;
            }
        }
    }

    private async Task<(string endpoint, string model)> GetConfigAsync(CancellationToken cancellationToken)
    {
        var config = await _dbContext.SystemConfiguration
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("System configuration not found");

        if (string.IsNullOrEmpty(config.OllamaEndpoint))
            throw new InvalidOperationException("Ollama endpoint is not configured");

        if (string.IsNullOrEmpty(config.OllamaModel))
            throw new InvalidOperationException("Ollama model is not configured");

        return (config.OllamaEndpoint, config.OllamaModel);
    }

    private static List<OllamaMessage> BuildMessages(
        IEnumerable<ChatMessageDto> conversationHistory,
        string? systemPrompt)
    {
        var messages = new List<OllamaMessage>();

        if (!string.IsNullOrEmpty(systemPrompt))
        {
            messages.Add(new OllamaMessage { Role = "system", Content = systemPrompt });
        }

        foreach (var msg in conversationHistory)
        {
            var role = msg.Role switch
            {
                MessageRole.User => "user",
                MessageRole.Assistant => "assistant",
                MessageRole.System => "system",
                _ => throw new ArgumentException($"Unknown message role: {msg.Role}")
            };

            var content = msg.Content;

            // Handle attachments by appending text descriptions
            if (msg.Attachments?.Any() == true)
            {
                var attachmentTexts = new StringBuilder(content);
                foreach (var attachment in msg.Attachments)
                {
                    // Ollama doesn't support native image/document attachments via API
                    // For text-based and Excel attachments, extract and include the content
                    if (IsTextBasedMimeType(attachment.MimeType))
                    {
                        try
                        {
                            var bytes = Convert.FromBase64String(attachment.Base64Data);
                            var text = Encoding.UTF8.GetString(bytes);
                            attachmentTexts.AppendLine($"\n\n--- Content from attached file: {attachment.FileName} ---\n{text}\n--- End of {attachment.FileName} ---");
                        }
                        catch
                        {
                            attachmentTexts.AppendLine($"\n\n[Attachment: {attachment.FileName} - unable to extract content]");
                        }
                    }
                    else if (IsExcelMimeType(attachment.MimeType))
                    {
                        try
                        {
                            var bytes = Convert.FromBase64String(attachment.Base64Data);
                            var text = ExtractExcelText(bytes, attachment.FileName);
                            attachmentTexts.AppendLine($"\n\n--- Content from attached file: {attachment.FileName} ---\n{text}\n--- End of {attachment.FileName} ---");
                        }
                        catch
                        {
                            attachmentTexts.AppendLine($"\n\n[Attachment: {attachment.FileName} - unable to extract content]");
                        }
                    }
                    else
                    {
                        attachmentTexts.AppendLine($"\n\n[Attachment: {attachment.FileName} ({attachment.MimeType}) - content not shown]");
                    }
                }
                content = attachmentTexts.ToString();
            }

            messages.Add(new OllamaMessage { Role = role, Content = content });
        }

        return messages;
    }

    private static bool IsTextBasedMimeType(string mimeType) =>
        mimeType.ToLowerInvariant() switch
        {
            "text/plain" => true,
            "text/csv" => true,
            "text/markdown" => true,
            "application/json" => true,
            _ => false
        };

    private static bool IsExcelMimeType(string mimeType) =>
        mimeType.ToLowerInvariant() switch
        {
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => true,
            "application/vnd.ms-excel" => true,
            _ => false
        };

    private static string ExtractExcelText(byte[] bytes, string fileName)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            using var workbook = new ClosedXML.Excel.XLWorkbook(stream);
            var sb = new StringBuilder();

            sb.AppendLine($"Excel Workbook: {fileName}");
            sb.AppendLine($"Sheets: {workbook.Worksheets.Count}");
            sb.AppendLine(new string('=', 50));

            foreach (var worksheet in workbook.Worksheets)
            {
                sb.AppendLine();
                sb.AppendLine($"Sheet: {worksheet.Name}");
                sb.AppendLine(new string('-', 40));

                var usedRange = worksheet.RangeUsed();
                if (usedRange == null)
                {
                    sb.AppendLine("[Empty sheet]");
                    continue;
                }

                var firstRow = usedRange.FirstRow().RowNumber();
                var lastRow = Math.Min(usedRange.LastRow().RowNumber(), firstRow + 499);
                var firstCol = usedRange.FirstColumn().ColumnNumber();
                var lastCol = usedRange.LastColumn().ColumnNumber();

                var headers = new List<string>();
                for (int col = firstCol; col <= lastCol; col++)
                {
                    var cell = worksheet.Cell(firstRow, col);
                    headers.Add(cell.GetString().Trim());
                }

                var rowCount = 0;
                for (int row = firstRow + 1; row <= lastRow; row++)
                {
                    sb.AppendLine($"Row {rowCount + 1}:");

                    for (int col = firstCol; col <= lastCol; col++)
                    {
                        var cell = worksheet.Cell(row, col);
                        var value = cell.GetString().Trim();
                        var headerIndex = col - firstCol;
                        var header = headerIndex < headers.Count ? headers[headerIndex] : $"Column{col}";

                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            sb.AppendLine($"  {header}: {value}");
                        }
                    }

                    sb.AppendLine();
                    rowCount++;
                }

                if (usedRange.LastRow().RowNumber() > lastRow)
                {
                    sb.AppendLine($"[Note: Showing first 500 of {usedRange.LastRow().RowNumber() - firstRow} total rows]");
                }
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"[Unable to parse Excel file {fileName}: {ex.Message}]";
        }
    }
}

// Ollama API DTOs
internal class OllamaChatRequest
{
    public string Model { get; set; } = string.Empty;
    public List<OllamaMessage> Messages { get; set; } = new();
    public bool Stream { get; set; }
}

internal class OllamaMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

internal class OllamaChatResponse
{
    public string Model { get; set; } = string.Empty;
    public OllamaMessage? Message { get; set; }
    public bool Done { get; set; }
}
