using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;

namespace DataChat.Web.Services.Setup;

/// <summary>
/// Parameters for building a SQL Server connection string
/// </summary>
public class ConnectionStringParameters
{
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = "DataChat";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool IntegratedSecurity { get; set; }
    public bool TrustServerCertificate { get; set; } = true;
    public int ConnectionTimeout { get; set; } = 30;
}

/// <summary>
/// Result of testing a database connection
/// </summary>
public class ConnectionTestResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ServerVersion { get; set; }
    public bool DatabaseExists { get; set; }
    public bool CanCreateDatabase { get; set; }
    public int? SqlErrorNumber { get; set; }
}

/// <summary>
/// Service for managing database connection strings
/// </summary>
public interface IConnectionStringService
{
    /// <summary>Builds a connection string from parameters</summary>
    string BuildConnectionString(ConnectionStringParameters parameters);

    /// <summary>Parses an existing connection string into parameters</summary>
    ConnectionStringParameters ParseConnectionString(string connectionString);

    /// <summary>Tests database connectivity</summary>
    Task<ConnectionTestResult> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default);

    /// <summary>Tests connectivity with parameters</summary>
    Task<ConnectionTestResult> TestConnectionAsync(ConnectionStringParameters parameters, CancellationToken cancellationToken = default);

    /// <summary>Saves the connection string to the appropriate settings file</summary>
    Task<bool> SaveConnectionStringAsync(string connectionString, CancellationToken cancellationToken = default);

    /// <summary>Creates the database if it doesn't exist</summary>
    Task<(bool Success, string? Error)> EnsureDatabaseExistsAsync(string connectionString, CancellationToken cancellationToken = default);
}

public class ConnectionStringService : IConnectionStringService
{
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConnectionStringService> _logger;

    public ConnectionStringService(
        IWebHostEnvironment environment,
        IConfiguration configuration,
        ILogger<ConnectionStringService> logger)
    {
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
    }

    public string BuildConnectionString(ConnectionStringParameters parameters)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = parameters.Server,
            InitialCatalog = parameters.Database,
            TrustServerCertificate = parameters.TrustServerCertificate,
            ConnectTimeout = parameters.ConnectionTimeout,
            MultipleActiveResultSets = true
        };

        if (parameters.IntegratedSecurity)
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = parameters.Username;
            builder.Password = parameters.Password;
        }

        return builder.ConnectionString;
    }

    public ConnectionStringParameters ParseConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new ConnectionStringParameters();
        }

        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            return new ConnectionStringParameters
            {
                Server = builder.DataSource,
                Database = builder.InitialCatalog,
                Username = builder.UserID,
                Password = builder.Password,
                IntegratedSecurity = builder.IntegratedSecurity,
                TrustServerCertificate = builder.TrustServerCertificate,
                ConnectionTimeout = builder.ConnectTimeout
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse connection string");
            return new ConnectionStringParameters();
        }
    }

    public Task<ConnectionTestResult> TestConnectionAsync(ConnectionStringParameters parameters, CancellationToken cancellationToken = default)
    {
        var connectionString = BuildConnectionString(parameters);
        return TestConnectionAsync(connectionString, cancellationToken);
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var result = new ConnectionTestResult();

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            result.Success = true;
            result.ServerVersion = connection.ServerVersion;

            // Check if specific database exists
            var builder = new SqlConnectionStringBuilder(connectionString);
            var dbName = builder.InitialCatalog;

            if (!string.IsNullOrEmpty(dbName))
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT DB_ID(@dbName)";
                command.Parameters.AddWithValue("@dbName", dbName);
                var dbId = await command.ExecuteScalarAsync(cancellationToken);
                result.DatabaseExists = dbId != DBNull.Value && dbId != null;

                // Check if user can create database (by checking for sysadmin or dbcreator role)
                await using var permCommand = connection.CreateCommand();
                permCommand.CommandText = @"
                    SELECT CASE
                        WHEN IS_SRVROLEMEMBER('sysadmin') = 1 THEN 1
                        WHEN IS_SRVROLEMEMBER('dbcreator') = 1 THEN 1
                        ELSE 0
                    END";
                var canCreate = await permCommand.ExecuteScalarAsync(cancellationToken);
                result.CanCreateDatabase = Convert.ToInt32(canCreate) == 1;
            }

            _logger.LogInformation("Connection test successful. Server: {Version}, DB exists: {Exists}",
                result.ServerVersion, result.DatabaseExists);
        }
        catch (SqlException ex)
        {
            result.Success = false;
            result.SqlErrorNumber = ex.Number;
            result.ErrorMessage = TranslateSqlError(ex);
            _logger.LogWarning(ex, "Connection test failed. SQL Error: {Number}", ex.Number);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Connection failed: {ex.Message}";
            _logger.LogWarning(ex, "Connection test failed");
        }

        return result;
    }

    public async Task<bool> SaveConnectionStringAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        try
        {
            // Determine which settings file to use based on environment
            var settingsFile = _environment.IsDevelopment()
                ? "appsettings.Development.json"
                : "appsettings.Production.json";

            var settingsPath = Path.Combine(_environment.ContentRootPath, settingsFile);

            // Read existing or create new
            JsonObject settings;
            if (File.Exists(settingsPath))
            {
                var json = await File.ReadAllTextAsync(settingsPath, cancellationToken);
                settings = JsonNode.Parse(json) as JsonObject ?? new JsonObject();
            }
            else
            {
                settings = new JsonObject();
            }

            // Ensure ConnectionStrings section exists
            if (!settings.ContainsKey("ConnectionStrings"))
            {
                settings["ConnectionStrings"] = new JsonObject();
            }

            settings["ConnectionStrings"]!["DefaultConnection"] = connectionString;

            // Write back with formatting
            var options = new JsonSerializerOptions { WriteIndented = true };
            var newJson = settings.ToJsonString(options);
            await File.WriteAllTextAsync(settingsPath, newJson, cancellationToken);

            _logger.LogInformation("Connection string saved to {SettingsFile}", settingsFile);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save connection string");
            return false;
        }
    }

    public async Task<(bool Success, string? Error)> EnsureDatabaseExistsAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            var dbName = builder.InitialCatalog;

            if (string.IsNullOrEmpty(dbName))
            {
                return (false, "Database name not specified in connection string");
            }

            // Connect to master to check/create database
            builder.InitialCatalog = "master";
            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            // Check if database exists
            await using var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = "SELECT DB_ID(@dbName)";
            checkCommand.Parameters.AddWithValue("@dbName", dbName);
            var dbId = await checkCommand.ExecuteScalarAsync(cancellationToken);

            if (dbId != DBNull.Value && dbId != null)
            {
                _logger.LogInformation("Database {Database} already exists", dbName);
                return (true, null);
            }

            // Create database
            await using var createCommand = connection.CreateCommand();
            // Use parameterized query for the database name would be ideal, but CREATE DATABASE
            // doesn't support parameters, so we validate the name first
            if (!IsValidDatabaseName(dbName))
            {
                return (false, "Invalid database name");
            }

            createCommand.CommandText = $"CREATE DATABASE [{dbName}]";
            await createCommand.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogInformation("Database {Database} created successfully", dbName);
            return (true, null);
        }
        catch (SqlException ex)
        {
            var error = TranslateSqlError(ex);
            _logger.LogError(ex, "Failed to create database");
            return (false, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create database");
            return (false, ex.Message);
        }
    }

    private static bool IsValidDatabaseName(string name)
    {
        // Basic validation to prevent SQL injection
        if (string.IsNullOrWhiteSpace(name) || name.Length > 128)
            return false;

        // Only allow alphanumeric, underscore, and hyphen
        return name.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-');
    }

    private static string TranslateSqlError(SqlException ex)
    {
        return ex.Number switch
        {
            -1 => "Could not connect to the server. Please verify the server name and ensure SQL Server is running.",
            -2 => "Connection timed out. The server may be slow or unreachable.",
            2 => "Could not connect to the server. Please verify the server name is correct.",
            53 => "Could not connect to the server. The server may be offline or the network path is incorrect.",
            18456 => "Login failed. Please check your username and password.",
            4060 => "Cannot open database. The database may not exist or you don't have access.",
            4063 => "Cannot open database. The database may not exist.",
            1045 => "Access denied. Please check your credentials.",
            40615 => "Cannot connect to Azure SQL Database. Please check firewall rules and server name.",
            40532 => "Cannot connect to Azure SQL Database. The server or database may not exist.",
            233 => "Connection was closed by the server. This may indicate a TLS/SSL configuration issue.",
            10054 => "Connection was forcibly closed. The server may have restarted.",
            10061 => "Connection refused. SQL Server may not be running or is not accepting connections.",
            _ => $"Database error ({ex.Number}): {ex.Message}"
        };
    }
}
