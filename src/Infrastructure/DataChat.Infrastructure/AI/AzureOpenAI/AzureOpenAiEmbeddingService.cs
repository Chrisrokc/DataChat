using Azure;
using Azure.AI.OpenAI;
using DataChat.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenAI.Embeddings;

namespace DataChat.Infrastructure.AI.AzureOpenAI;

public class AzureOpenAiEmbeddingService : IEmbeddingService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ISecureConfigurationService _secureConfig;
    private readonly IAiResiliencePipeline _resilience;
    private readonly ILogger<AzureOpenAiEmbeddingService> _logger;

    // text-embedding-ada-002 and text-embedding-3-small/large have 8192 token limit
    private const int MaxTokens = 8000;
    private const int CharsPerToken = 4;

    public AzureOpenAiEmbeddingService(
        IApplicationDbContext dbContext,
        ISecureConfigurationService secureConfig,
        IAiResiliencePipeline resilience,
        ILogger<AzureOpenAiEmbeddingService> logger)
    {
        _dbContext = dbContext;
        _secureConfig = secureConfig;
        _resilience = resilience;
        _logger = logger;
    }

    public async Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var client = await GetClientAsync(cancellationToken);

        var truncatedText = TruncateToTokenLimit(text);

        // Execute with resilience (retry + circuit breaker)
        var embedding = await _resilience.ExecuteAsync(
            async ct => await client.GenerateEmbeddingAsync(truncatedText, cancellationToken: ct),
            cancellationToken);

        return embedding.Value.ToFloats().ToArray();
    }

    public async Task<IEnumerable<float[]>> GenerateEmbeddingsAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default)
    {
        var client = await GetClientAsync(cancellationToken);

        var textList = texts.Select(TruncateToTokenLimit).ToList();

        if (!textList.Any())
            return Enumerable.Empty<float[]>();

        // Process in batches of 100 (Azure OpenAI limit)
        var results = new List<float[]>();
        const int batchSize = 100;

        for (int i = 0; i < textList.Count; i += batchSize)
        {
            var batch = textList.Skip(i).Take(batchSize).ToList();

            // Execute with resilience (retry + circuit breaker)
            var embeddings = await _resilience.ExecuteAsync(
                async ct => await client.GenerateEmbeddingsAsync(batch, cancellationToken: ct),
                cancellationToken);

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

        var maxChars = MaxTokens * CharsPerToken;

        _logger.LogWarning(
            "Text truncated from ~{OriginalTokens} tokens to {MaxTokens} tokens for embedding",
            estimatedTokens, MaxTokens);

        var truncated = text[..maxChars];
        var lastSpace = truncated.LastIndexOf(' ');

        if (lastSpace > maxChars * 0.9)
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

        if (string.IsNullOrEmpty(config.AzureOpenAiEndpoint))
            throw new InvalidOperationException("Azure OpenAI endpoint is not configured");

        if (string.IsNullOrEmpty(config.AzureOpenAiApiKey))
            throw new InvalidOperationException("Azure OpenAI API key is not configured");

        if (string.IsNullOrEmpty(config.AzureOpenAiEmbeddingDeployment))
            throw new InvalidOperationException("Azure OpenAI embedding deployment is not configured");

        var apiKey = _secureConfig.Decrypt(config.AzureOpenAiApiKey);
        var endpoint = new Uri(config.AzureOpenAiEndpoint);

        var azureClient = new AzureOpenAIClient(endpoint, new AzureKeyCredential(apiKey));
        return azureClient.GetEmbeddingClient(config.AzureOpenAiEmbeddingDeployment);
    }
}
