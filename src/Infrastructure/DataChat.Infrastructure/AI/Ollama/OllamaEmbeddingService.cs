using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DataChat.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataChat.Infrastructure.AI.Ollama;

public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OllamaEmbeddingService> _logger;

    // Target embedding dimension (must match database VECTOR column)
    private const int TargetDimension = 1536;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public OllamaEmbeddingService(
        IApplicationDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        ILogger<OllamaEmbeddingService> logger)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var (endpoint, model) = await GetConfigAsync(cancellationToken);
        var client = _httpClientFactory.CreateClient("Ollama");
        client.BaseAddress = new Uri(endpoint);

        var request = new OllamaEmbedRequest
        {
            Model = model,
            Input = text
        };

        var response = await client.PostAsJsonAsync("/api/embed", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>(JsonOptions, cancellationToken);

        if (result?.Embeddings == null || result.Embeddings.Count == 0)
            throw new InvalidOperationException("Ollama returned no embeddings");

        var embedding = result.Embeddings[0];
        return NormalizeEmbeddingDimension(embedding);
    }

    public async Task<IEnumerable<float[]>> GenerateEmbeddingsAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default)
    {
        var textList = texts.ToList();
        if (!textList.Any())
            return Enumerable.Empty<float[]>();

        var (endpoint, model) = await GetConfigAsync(cancellationToken);
        var client = _httpClientFactory.CreateClient("Ollama");
        client.BaseAddress = new Uri(endpoint);

        // Ollama supports batch embeddings
        var request = new OllamaEmbedBatchRequest
        {
            Model = model,
            Input = textList
        };

        var response = await client.PostAsJsonAsync("/api/embed", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>(JsonOptions, cancellationToken);

        if (result?.Embeddings == null)
            throw new InvalidOperationException("Ollama returned no embeddings");

        return result.Embeddings.Select(NormalizeEmbeddingDimension).ToList();
    }

    private float[] NormalizeEmbeddingDimension(float[] embedding)
    {
        // If embedding is already the target dimension, return as-is
        if (embedding.Length == TargetDimension)
            return embedding;

        // If embedding is smaller, pad with zeros
        if (embedding.Length < TargetDimension)
        {
            _logger.LogWarning(
                "Ollama embedding has {ActualDim} dimensions, padding to {TargetDim} dimensions",
                embedding.Length, TargetDimension);

            var padded = new float[TargetDimension];
            Array.Copy(embedding, padded, embedding.Length);
            return padded;
        }

        // If embedding is larger, truncate
        _logger.LogWarning(
            "Ollama embedding has {ActualDim} dimensions, truncating to {TargetDim} dimensions",
            embedding.Length, TargetDimension);

        return embedding.Take(TargetDimension).ToArray();
    }

    private async Task<(string endpoint, string model)> GetConfigAsync(CancellationToken cancellationToken)
    {
        var config = await _dbContext.SystemConfiguration
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("System configuration not found");

        if (string.IsNullOrEmpty(config.OllamaEndpoint))
            throw new InvalidOperationException("Ollama endpoint is not configured");

        if (string.IsNullOrEmpty(config.OllamaEmbeddingModel))
            throw new InvalidOperationException("Ollama embedding model is not configured");

        return (config.OllamaEndpoint, config.OllamaEmbeddingModel);
    }
}

// Ollama Embedding API DTOs
internal class OllamaEmbedRequest
{
    public string Model { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
}

internal class OllamaEmbedBatchRequest
{
    public string Model { get; set; } = string.Empty;
    public List<string> Input { get; set; } = new();
}

internal class OllamaEmbedResponse
{
    public string Model { get; set; } = string.Empty;
    public List<float[]> Embeddings { get; set; } = new();
}
