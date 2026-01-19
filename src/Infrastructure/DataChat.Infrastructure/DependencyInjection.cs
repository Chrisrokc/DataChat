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
        // Database
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        services.AddDbContextFactory<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, sqlOptions =>
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

        // AI Services - Register all provider implementations
        services.AddScoped<OpenAiChatService>();
        services.AddScoped<OpenAiEmbeddingService>();
        services.AddScoped<AzureOpenAiChatService>();
        services.AddScoped<AzureOpenAiEmbeddingService>();
        services.AddScoped<OllamaChatService>();
        services.AddScoped<OllamaEmbeddingService>();

        // AI Service Factory
        services.AddScoped<IAiServiceFactory, AiServiceFactory>();

        // Register interfaces via factory
        services.AddScoped<IAiChatService>(sp =>
            sp.GetRequiredService<IAiServiceFactory>().GetChatService());
        services.AddScoped<IEmbeddingService>(sp =>
            sp.GetRequiredService<IAiServiceFactory>().GetEmbeddingService());

        // HttpClient for Ollama
        services.AddHttpClient("Ollama");

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

        // HTTP Context
        services.AddHttpContextAccessor();

        return services;
    }
}
