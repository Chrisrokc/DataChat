using DataChat.Application.Common.Interfaces;
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
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
            }));

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());

        // Note: Data Protection is configured in Program.cs to ensure single registration

        // Services
        services.AddScoped<IDateTimeService, DateTimeService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ISecureConfigurationService, SecureConfigurationService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IDatabaseConnectionService, DatabaseConnectionService>();

        // AI Services
        services.AddScoped<IAiChatService, OpenAiChatService>();
        services.AddScoped<IEmbeddingService, OpenAiEmbeddingService>();

        // Vector Store
        services.AddScoped<IVectorStore, SqlServerVectorStore>();

        // Document Processing
        services.AddScoped<IDocumentParser, PdfParser>();
        services.AddScoped<IDocumentParser, WordDocumentParser>();
        services.AddScoped<IDocumentParser, TextFileParser>();
        services.AddScoped<IDocumentParserFactory, DocumentParserFactory>();
        services.AddScoped<IChunkingStrategy, RecursiveChunkingStrategy>();

        // Document Sync Service (singleton to maintain job state across requests)
        services.AddSingleton<IDocumentSyncService, DocumentSyncService>();

        // HTTP Context
        services.AddHttpContextAccessor();

        return services;
    }
}
