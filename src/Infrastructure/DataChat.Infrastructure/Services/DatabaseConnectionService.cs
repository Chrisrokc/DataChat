using DataChat.Application.Common.Interfaces;
using DataChat.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataChat.Infrastructure.Services;

public class DatabaseConnectionService : IDatabaseConnectionService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ISecureConfigurationService _secureConfig;
    private readonly ILogger<DatabaseConnectionService> _logger;

    public DatabaseConnectionService(
        IApplicationDbContext dbContext,
        ISecureConfigurationService secureConfig,
        ILogger<DatabaseConnectionService> logger)
    {
        _dbContext = dbContext;
        _secureConfig = secureConfig;
        _logger = logger;
    }

    public async Task<DatabaseConnectionTestResult> TestConnectionAsync(
        string serverHost,
        int port,
        string databaseName,
        bool useWindowsAuth,
        string? username,
        string? password,
        bool trustServerCertificate,
        int connectionTimeout,
        CancellationToken cancellationToken = default)
    {
        var connectionString = BuildConnectionString(
            serverHost, port, databaseName, useWindowsAuth,
            username, password, trustServerCertificate, connectionTimeout);

        return await TestConnectionInternalAsync(connectionString, cancellationToken);
    }

    public async Task<DatabaseConnectionTestResult> TestConnectionAsync(
        Guid connectionId,
        string? passwordOverride = null,
        CancellationToken cancellationToken = default)
    {
        var connection = await _dbContext.DatabaseConnections
            .FirstOrDefaultAsync(c => c.Id == connectionId, cancellationToken);

        if (connection == null)
        {
            return new DatabaseConnectionTestResult(false, null, null, "Connection not found");
        }

        string? password = null;
        if (!connection.UseWindowsAuth)
        {
            password = passwordOverride ??
                (string.IsNullOrEmpty(connection.EncryptedPassword)
                    ? null
                    : _secureConfig.Decrypt(connection.EncryptedPassword));
        }

        var connectionString = BuildConnectionString(connection, password);
        return await TestConnectionInternalAsync(connectionString, cancellationToken);
    }

    private async Task<DatabaseConnectionTestResult> TestConnectionInternalAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            // Get server version and database name
            await using var cmd = new SqlCommand(
                "SELECT @@VERSION AS ServerVersion, DB_NAME() AS DatabaseName",
                connection);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            if (await reader.ReadAsync(cancellationToken))
            {
                var serverVersion = reader.GetString(0);
                var dbName = reader.GetString(1);

                // Extract just the version line (first line)
                var versionLine = serverVersion.Split('\n')[0].Trim();

                _logger.LogInformation(
                    "Database connection test successful. Server: {ServerVersion}, Database: {Database}",
                    versionLine, dbName);

                return new DatabaseConnectionTestResult(true, versionLine, dbName, null);
            }

            return new DatabaseConnectionTestResult(true, "Unknown", "Unknown", null);
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(ex, "Database connection test failed");
            return new DatabaseConnectionTestResult(false, null, null, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during database connection test");
            return new DatabaseConnectionTestResult(false, null, null, $"Unexpected error: {ex.Message}");
        }
    }

    public async Task<IEnumerable<DatabaseObjectInfo>> GetTablesAndViewsAsync(
        Guid connectionId,
        CancellationToken cancellationToken = default)
    {
        var connection = await _dbContext.DatabaseConnections
            .FirstOrDefaultAsync(c => c.Id == connectionId, cancellationToken);

        if (connection == null)
        {
            throw new InvalidOperationException($"Database connection with ID {connectionId} not found");
        }

        string? password = null;
        if (!connection.UseWindowsAuth && !string.IsNullOrEmpty(connection.EncryptedPassword))
        {
            password = _secureConfig.Decrypt(connection.EncryptedPassword);
        }

        return await GetTablesAndViewsInternalAsync(
            BuildConnectionString(connection, password),
            cancellationToken);
    }

    public async Task<IEnumerable<DatabaseObjectInfo>> GetTablesAndViewsAsync(
        string serverHost,
        int port,
        string databaseName,
        bool useWindowsAuth,
        string? username,
        string? password,
        bool trustServerCertificate,
        int connectionTimeout,
        CancellationToken cancellationToken = default)
    {
        var connectionString = BuildConnectionString(
            serverHost, port, databaseName, useWindowsAuth,
            username, password, trustServerCertificate, connectionTimeout);

        return await GetTablesAndViewsInternalAsync(connectionString, cancellationToken);
    }

    private async Task<IEnumerable<DatabaseObjectInfo>> GetTablesAndViewsInternalAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        var results = new List<DatabaseObjectInfo>();

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            const string query = @"
                SELECT
                    TABLE_SCHEMA AS SchemaName,
                    TABLE_NAME AS ObjectName,
                    TABLE_TYPE AS ObjectType
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_TYPE IN ('BASE TABLE', 'VIEW')
                ORDER BY TABLE_SCHEMA, TABLE_TYPE DESC, TABLE_NAME";

            await using var cmd = new SqlCommand(query, connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new DatabaseObjectInfo(
                    reader.GetString(0), // SchemaName
                    reader.GetString(1), // ObjectName
                    reader.GetString(2)  // ObjectType
                ));
            }

            _logger.LogInformation(
                "Retrieved {Count} tables and views from database",
                results.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve tables and views");
            throw;
        }

        return results;
    }

    public string BuildConnectionString(DatabaseConnection connection, string? decryptedPassword = null)
    {
        string? password = decryptedPassword;

        if (password == null && !connection.UseWindowsAuth && !string.IsNullOrEmpty(connection.EncryptedPassword))
        {
            try
            {
                password = _secureConfig.Decrypt(connection.EncryptedPassword);
            }
            catch
            {
                // If decryption fails, password remains null
            }
        }

        return BuildConnectionString(
            connection.ServerHost,
            connection.Port,
            connection.DatabaseName,
            connection.UseWindowsAuth,
            connection.Username,
            password,
            connection.TrustServerCertificate,
            connection.ConnectionTimeout);
    }

    public string BuildConnectionString(
        string serverHost,
        int port,
        string databaseName,
        bool useWindowsAuth,
        string? username,
        string? password,
        bool trustServerCertificate,
        int connectionTimeout)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = port != 1433 ? $"{serverHost},{port}" : serverHost,
            InitialCatalog = databaseName,
            TrustServerCertificate = trustServerCertificate,
            ConnectTimeout = connectionTimeout
        };

        if (useWindowsAuth)
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = username;
            builder.Password = password;
        }

        return builder.ConnectionString;
    }
}
