using DataChat.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Embeddings;

namespace DataChat.Infrastructure.AI.OpenAI;

public class OpenAiEmbeddingService : IEmbeddingService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ISecureConfigurationService _secureConfig;
    private readonly ILogger<OpenAiEmbeddingService> _logger;

    // text-embedding-ada-002 and text-embedding-3-small/large have 8192 token limit
    // Use conservative limit to account for tokenization differences
    private const int MaxTokens = 8000;
    private const int CharsPerToken = 4; // Conservative estimate

    public OpenAiEmbeddingService(
        IApplicationDbContext dbContext,
        ISecureConfigurationService secureConfig,
        ILogger<OpenAiEmbeddingService> logger)
    {
        _dbContext = dbContext;
        _secureConfig = secureConfig;
        _logger = logger;
    }

    public async Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var client = await GetClientAsync(cancellationToken);

        // Truncate text if it exceeds token limit
        var truncatedText = TruncateToTokenLimit(text);

        var embedding = await client.GenerateEmbeddingAsync(truncatedText, cancellationToken: cancellationToken);

        return embedding.Value.ToFloats().ToArray();
    }

    public async Task<IEnumerable<float[]>> GenerateEmbeddingsAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default)
    {
        var client = await GetClientAsync(cancellationToken);

        // Truncate each text to token limit
        var textList = texts.Select(TruncateToTokenLimit).ToList();

        if (!textList.Any())
            return Enumerable.Empty<float[]>();

        // Process in batches of 100 (OpenAI limit)
        var results = new List<float[]>();
        const int batchSize = 100;

        for (int i = 0; i < textList.Count; i += batchSize)
        {
            var batch = textList.Skip(i).Take(batchSize).ToList();
            var embeddings = await client.GenerateEmbeddingsAsync(batch, cancellationToken: cancellationToken);

            foreach (var embedding in embeddings.Value)
            {
                results.Add(embedding.ToFloats().ToArray());
            }
        }

        return results;
    }

    private string TruncateToTokenLimit(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var estimatedTokens = text.Length / CharsPerToken;

        if (estimatedTokens <= MaxTokens)
            return text;

        // Truncate to max characters (with buffer for safety)
        var maxChars = MaxTokens * CharsPerToken;

        _logger.LogWarning(
            "Text truncated from ~{OriginalTokens} tokens to {MaxTokens} tokens for embedding",
            estimatedTokens, MaxTokens);

        // Try to truncate at a word boundary
        var truncated = text[..maxChars];
        var lastSpace = truncated.LastIndexOf(' ');

        if (lastSpace > maxChars * 0.9) // Only use word boundary if it's within 90% of max
        {
            return truncated[..lastSpace];
        }

        return truncated;
    }

    private async Task<EmbeddingClient> GetClientAsync(CancellationToken cancellationToken)
    {
        var config = await _dbContext.SystemConfiguration
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("System configuration not found");

        if (string.IsNullOrEmpty(config.OpenAiApiKey))
            throw new InvalidOperationException("OpenAI API key is not configured");

        var apiKey = _secureConfig.Decrypt(config.OpenAiApiKey);
        var openAiClient = new OpenAIClient(apiKey);

        return openAiClient.GetEmbeddingClient(config.EmbeddingModel);
    }
}
