using DataChat.Domain.Entities;
using DataChat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace DataChat.Infrastructure.Services;

/// <summary>
/// Provides cached access to system configuration to reduce database queries.
/// Configuration is cached for 5 minutes and can be invalidated when changes are made.
/// </summary>
public interface ICachedConfigurationService
{
    /// <summary>
    /// Gets the system configuration from cache or database.
    /// </summary>
    Task<SystemConfiguration?> GetSystemConfigurationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates the cached configuration, forcing a fresh read on next access.
    /// Call this when configuration is updated.
    /// </summary>
    void InvalidateCache();
}

public class CachedConfigurationService : ICachedConfigurationService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedConfigurationService> _logger;

    private const string CacheKey = "SystemConfiguration";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public CachedConfigurationService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        IMemoryCache cache,
        ILogger<CachedConfigurationService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<SystemConfiguration?> GetSystemConfigurationAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out SystemConfiguration? cachedConfig))
        {
            return cachedConfig;
        }

        _logger.LogDebug("System configuration cache miss, loading from database");

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var config = await dbContext.SystemConfiguration
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (config != null)
        {
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(CacheDuration)
                .SetSize(1); // Use size-based eviction

            _cache.Set(CacheKey, config, cacheOptions);
            _logger.LogDebug("System configuration cached for {Duration}", CacheDuration);
        }

        return config;
    }

    public void InvalidateCache()
    {
        _cache.Remove(CacheKey);
        _logger.LogInformation("System configuration cache invalidated");
    }
}
