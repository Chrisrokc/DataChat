using DataChat.Application.Common.Interfaces;
using DataChat.Infrastructure.AI;
using DataChat.Infrastructure.AI.AzureOpenAI;
using DataChat.Infrastructure.AI.Ollama;
using DataChat.Infrastructure.AI.OpenAI;
using DataChat.Infrastructure.DocumentProcessing.Chunking;
using DataChat.Infrastructure.DocumentProcessing.Parsers;
using DataChat.Infrastructure.Identity;
using DataChat.Infrastructure.Persistence;
using DataChat.Infrastructure.Services;
using DataChat.Infrastructure.VectorStore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataChat.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database with explicit connection pool settings for 40-50 concurrent users
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        // Configure connection pooling explicitly for scalability
        var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString ?? "")
        {
            MinPoolSize = 10,           // Pre-warm connections
            MaxPoolSize = 100,          // Support 40-50 concurrent users with headroom
            ConnectTimeout = 30,        // Wait up to 30 seconds for connection
            Pooling = true              // Ensure pooling is enabled
        };

        services.AddDbContextFactory<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionStringBuilder.ConnectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null);
                sqlOptions.CommandTimeout(60);
            }));

        // Also register the DbContext itself for backward compatibility
        services.AddScoped<ApplicationDbContext>(provider =>
            provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());

        // Note: Data Protection is configured in Program.cs to ensure single registration

        // Services
        services.AddScoped<IDateTimeService, DateTimeService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ISecureConfigurationService, SecureConfigurationService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IDatabaseConnectionService, DatabaseConnectionService>();
        services.AddScoped<IDocumentAccessTokenService, DocumentAccessTokenService>();
        services.AddScoped<IAuditService, AuditService>();

        // AI Services - Register all provider implementations
        services.AddScoped<OpenAiChatService>();
        services.AddScoped<OpenAiEmbeddingService>();
        services.AddScoped<AzureOpenAiChatService>();
        services.AddScoped<AzureOpenAiEmbeddingService>();
        services.AddScoped<OllamaChatService>();
        services.AddScoped<OllamaEmbeddingService>();

        // AI Service Factory
        services.AddScoped<IAiServiceFactory, AiServiceFactory>();

        // AI Request Queue for concurrency control (singleton to share across all requests)
        services.AddSingleton<IAiRequestQueue, AiRequestQueue>();

        // AI Resilience Pipeline for retry/circuit breaker (singleton to share circuit state)
        services.AddSingleton<IAiResiliencePipeline, AiResiliencePipeline>();

        // Register interfaces via factory
        services.AddScoped<IAiChatService>(sp =>
            sp.GetRequiredService<IAiServiceFactory>().GetChatService());
        services.AddScoped<IEmbeddingService>(sp =>
            sp.GetRequiredService<IAiServiceFactory>().GetEmbeddingService());

        // HttpClient for Ollama with resilience policies
        services.AddHttpClient("Ollama")
            .AddStandardResilienceHandler(options =>
            {
                // Configure retry
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.Delay = TimeSpan.FromSeconds(2);
                options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
                options.Retry.UseJitter = true;

                // Configure circuit breaker
                // SamplingDuration must be >= 2x AttemptTimeout (90s), so use 180s
                options.CircuitBreaker.FailureRatio = 0.5;
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(180);
                options.CircuitBreaker.MinimumThroughput = 5;
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);

                // Configure timeout
                options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(2);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(90);
            });

        // Vector Store
        services.AddScoped<IVectorStore, SqlServerVectorStore>();

        // Document Processing
        services.AddScoped<IDocumentParser, PdfParser>();
        services.AddScoped<IDocumentParser, WordDocumentParser>();
        services.AddScoped<IDocumentParser, TextFileParser>();
        services.AddScoped<IDocumentParser, ImageParser>();
        services.AddScoped<IDocumentParser, SpreadsheetParser>();
        services.AddScoped<IDocumentParserFactory, DocumentParserFactory>();
        services.AddScoped<IChunkingStrategy, RecursiveChunkingStrategy>();

        // Document Sync Service (singleton to maintain job state across requests)
        services.AddSingleton<IDocumentSyncService, DocumentSyncService>();

        // Personal Document Service
        services.AddScoped<IPersonalDocumentService, PersonalDocumentService>();

        // Configuration caching (singleton with in-memory cache)
        services.AddMemoryCache();
        services.AddSingleton<ICachedConfigurationService, CachedConfigurationService>();

        // HTTP Context
        services.AddHttpContextAccessor();

        return services;
    }
}
