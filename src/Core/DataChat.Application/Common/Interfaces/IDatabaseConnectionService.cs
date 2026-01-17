using DataChat.Domain.Entities;

namespace DataChat.Application.Common.Interfaces;

/// <summary>
/// Service for managing and testing SQL Server database connections.
/// </summary>
public interface IDatabaseConnectionService
{
    /// <summary>
    /// Tests a database connection with the provided parameters.
    /// </summary>
    Task<DatabaseConnectionTestResult> TestConnectionAsync(
        string serverHost,
        int port,
        string databaseName,
        bool useWindowsAuth,
        string? username,
        string? password,
        bool trustServerCertificate,
        int connectionTimeout,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests an existing saved database connection.
    /// </summary>
    Task<DatabaseConnectionTestResult> TestConnectionAsync(
        Guid connectionId,
        string? passwordOverride = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches all tables and views from a database using a saved connection.
    /// </summary>
    Task<IEnumerable<DatabaseObjectInfo>> GetTablesAndViewsAsync(
        Guid connectionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches all tables and views using explicit connection parameters.
    /// </summary>
    Task<IEnumerable<DatabaseObjectInfo>> GetTablesAndViewsAsync(
        string serverHost,
        int port,
        string databaseName,
        bool useWindowsAuth,
        string? username,
        string? password,
        bool trustServerCertificate,
        int connectionTimeout,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds a connection string from a saved DatabaseConnection entity.
    /// </summary>
    string BuildConnectionString(DatabaseConnection connection, string? decryptedPassword = null);

    /// <summary>
    /// Builds a connection string from explicit parameters.
    /// </summary>
    string BuildConnectionString(
        string serverHost,
        int port,
        string databaseName,
        bool useWindowsAuth,
        string? username,
        string? password,
        bool trustServerCertificate,
        int connectionTimeout);
}

/// <summary>
/// Result of a database connection test.
/// </summary>
public record DatabaseConnectionTestResult(
    bool Success,
    string? ServerVersion,
    string? DatabaseName,
    string? ErrorMessage);

/// <summary>
/// Information about a database table or view.
/// </summary>
public record DatabaseObjectInfo(
    string SchemaName,
    string ObjectName,
    string ObjectType); // "BASE TABLE" or "VIEW"
