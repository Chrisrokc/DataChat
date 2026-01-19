using DataChat.Application.Common.Interfaces;
using DataChat.Domain.Enums;
using DataChat.Infrastructure.AI.AzureOpenAI;
using DataChat.Infrastructure.AI.Ollama;
using DataChat.Infrastructure.AI.OpenAI;
using DataChat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DataChat.Infrastructure.AI;

public interface IAiServiceFactory
{
    IAiChatService GetChatService();
    IEmbeddingService GetEmbeddingService();
}

public class AiServiceFactory : IAiServiceFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly ILogger<AiServiceFactory> _logger;

    public AiServiceFactory(
        IServiceProvider serviceProvider,
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        ILogger<AiServiceFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public IAiChatService GetChatService()
    {
        var provider = GetConfiguredProvider();

        _logger.LogDebug("Creating chat service for provider: {Provider}", provider);

        return provider switch
        {
            LlmProvider.OpenAI => _serviceProvider.GetRequiredService<OpenAiChatService>(),
            LlmProvider.AzureOpenAI => _serviceProvider.GetRequiredService<AzureOpenAiChatService>(),
            LlmProvider.Ollama => _serviceProvider.GetRequiredService<OllamaChatService>(),
            _ => throw new InvalidOperationException($"Unknown LLM provider: {provider}")
        };
    }

    public IEmbeddingService GetEmbeddingService()
    {
        var provider = GetConfiguredProvider();

        _logger.LogDebug("Creating embedding service for provider: {Provider}", provider);

        return provider switch
        {
            LlmProvider.OpenAI => _serviceProvider.GetRequiredService<OpenAiEmbeddingService>(),
            LlmProvider.AzureOpenAI => _serviceProvider.GetRequiredService<AzureOpenAiEmbeddingService>(),
            LlmProvider.Ollama => _serviceProvider.GetRequiredService<OllamaEmbeddingService>(),
            _ => throw new InvalidOperationException($"Unknown LLM provider: {provider}")
        };
    }

    private LlmProvider GetConfiguredProvider()
    {
        // Use a fresh DbContext instance to avoid disposed context issues
        using var dbContext = _dbContextFactory.CreateDbContext();

        var config = dbContext.SystemConfiguration
            .AsNoTracking()
            .FirstOrDefault();

        if (config == null)
        {
            _logger.LogWarning("System configuration not found, defaulting to OpenAI");
            return LlmProvider.OpenAI;
        }

        return config.LlmProvider;
    }
}
