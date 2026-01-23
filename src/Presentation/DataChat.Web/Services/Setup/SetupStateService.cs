using System.Security.Cryptography;
using DataChat.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DataChat.Web.Services.Setup;

/// <summary>
/// Represents the current setup state of the application
/// </summary>
public enum SetupState
{
    /// <summary>Everything configured, normal operation</summary>
    Complete,
    /// <summary>No connection string configured</summary>
    ConnectionStringMissing,
    /// <summary>Connection string exists but can't connect</summary>
    DatabaseUnreachable,
    /// <summary>Connected but schema not applied</summary>
    MigrationsNeeded,
    /// <summary>Schema exists but no admin user</summary>
    AdminUserNeeded
}

/// <summary>
/// Service to detect and manage the setup state of the application
/// </summary>
public interface ISetupStateService
{
    /// <summary>Gets the current setup state</summary>
    Task<SetupState> GetCurrentStateAsync(CancellationToken cancellationToken = default);

    /// <summary>Checks if setup is required</summary>
    Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the setup token (generates one if not exists)</summary>
    string GetSetupToken();

    /// <summary>Validates a setup token</summary>
    bool ValidateSetupToken(string token);

    /// <summary>Invalidates the current setup token</summary>
    void InvalidateSetupToken();

    /// <summary>Clears the cached state to force re-evaluation</summary>
    void ClearCache();
}

public class SetupStateService : ISetupStateService
{
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<SetupStateService> _logger;

    private static string? _setupToken;
    private static DateTime _tokenGeneratedAt = DateTime.MinValue;
    private static readonly object _tokenLock = new();

    // Cache the state briefly to avoid repeated DB checks
    private static SetupState? _cachedState;
    private static DateTime _cacheExpiry = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(5);

    public SetupStateService(
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        IWebHostEnvironment environment,
        ILogger<SetupStateService> logger)
    {
        _configuration = configuration;
        _serviceProvider = serviceProvider;
        _environment = environment;
        _logger = logger;
    }

    public async Task<SetupState> GetCurrentStateAsync(CancellationToken cancellationToken = default)
    {
        // Check cache first
        if (_cachedState.HasValue && DateTime.UtcNow < _cacheExpiry)
        {
            return _cachedState.Value;
        }

        var state = await DetermineStateAsync(cancellationToken);
        _cachedState = state;
        _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);

        _logger.LogDebug("Setup state determined: {State}", state);
        return state;
    }

    public async Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default)
    {
        var state = await GetCurrentStateAsync(cancellationToken);
        return state != SetupState.Complete;
    }

    private async Task<SetupState> DetermineStateAsync(CancellationToken cancellationToken)
    {
        // 1. Check connection string
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogInformation("Setup required: Connection string is missing");
            return SetupState.ConnectionStringMissing;
        }

        // 2. Try to connect
        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            // 3. Check if database exists and we can query it
            // Try to check migrations - if this fails, we might need to create the DB
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Check pending migrations
                var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
                var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken);

                if (!appliedMigrations.Any())
                {
                    // No migrations applied at all - need initial setup
                    _logger.LogInformation("Setup required: No migrations applied");
                    return SetupState.MigrationsNeeded;
                }

                if (pendingMigrations.Any())
                {
                    _logger.LogInformation("Setup required: {Count} pending migrations", pendingMigrations.Count());
                    return SetupState.MigrationsNeeded;
                }

                // 4. Check for admin user
                var hasAdminUser = await dbContext.Users
                    .AnyAsync(u => u.UserRoles.Any(ur => ur.Role.Name == "Admin"), cancellationToken);

                if (!hasAdminUser)
                {
                    _logger.LogInformation("Setup required: No admin user exists");
                    return SetupState.AdminUserNeeded;
                }

                return SetupState.Complete;
            }
            catch (SqlException ex) when (ex.Number == 208) // Invalid object name - tables don't exist
            {
                _logger.LogInformation("Setup required: Database schema not created (tables missing)");
                return SetupState.MigrationsNeeded;
            }
            catch (SqlException ex) when (ex.Number == 4060) // Cannot open database
            {
                _logger.LogInformation("Setup required: Database does not exist");
                return SetupState.MigrationsNeeded;
            }
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(ex, "Setup required: Cannot connect to database (SQL Error: {Number})", ex.Number);
            return SetupState.DatabaseUnreachable;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Setup required: Database connectivity error");
            return SetupState.DatabaseUnreachable;
        }
    }

    public string GetSetupToken()
    {
        lock (_tokenLock)
        {
            var tokenExpiryMinutes = _configuration.GetValue<int>("Setup:TokenExpiryMinutes", 60);

            // Check if token exists and is still valid
            if (_setupToken != null && DateTime.UtcNow < _tokenGeneratedAt.AddMinutes(tokenExpiryMinutes))
            {
                return _setupToken;
            }

            // Generate new token
            var bytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            _setupToken = Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
            _tokenGeneratedAt = DateTime.UtcNow;

            // Also persist to file for restart scenarios
            try
            {
                var tokenPath = Path.Combine(_environment.ContentRootPath, ".setup-token");
                File.WriteAllText(tokenPath, $"{_setupToken}|{_tokenGeneratedAt:O}");
                _logger.LogDebug("Setup token generated and saved");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not persist setup token to file");
            }

            return _setupToken;
        }
    }

    public bool ValidateSetupToken(string token)
    {
        if (string.IsNullOrEmpty(token))
            return false;

        lock (_tokenLock)
        {
            // Try to load from file if not in memory
            if (_setupToken == null)
            {
                TryLoadTokenFromFile();
            }

            if (_setupToken == null)
                return false;

            var tokenExpiryMinutes = _configuration.GetValue<int>("Setup:TokenExpiryMinutes", 60);

            // Check expiry
            if (DateTime.UtcNow > _tokenGeneratedAt.AddMinutes(tokenExpiryMinutes))
            {
                _logger.LogWarning("Setup token has expired");
                return false;
            }

            return string.Equals(token, _setupToken, StringComparison.Ordinal);
        }
    }

    public void InvalidateSetupToken()
    {
        lock (_tokenLock)
        {
            _setupToken = null;
            _tokenGeneratedAt = DateTime.MinValue;

            try
            {
                var tokenPath = Path.Combine(_environment.ContentRootPath, ".setup-token");
                if (File.Exists(tokenPath))
                {
                    File.Delete(tokenPath);
                }
                _logger.LogInformation("Setup token invalidated");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete setup token file");
            }
        }
    }

    public void ClearCache()
    {
        _cachedState = null;
        _cacheExpiry = DateTime.MinValue;
    }

    private void TryLoadTokenFromFile()
    {
        try
        {
            var tokenPath = Path.Combine(_environment.ContentRootPath, ".setup-token");
            if (File.Exists(tokenPath))
            {
                var content = File.ReadAllText(tokenPath);
                var parts = content.Split('|');
                if (parts.Length == 2)
                {
                    _setupToken = parts[0];
                    _tokenGeneratedAt = DateTime.Parse(parts[1]);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load setup token from file");
        }
    }
}
