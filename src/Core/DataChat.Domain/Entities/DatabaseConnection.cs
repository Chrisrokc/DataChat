using DataChat.Domain.Common;

namespace DataChat.Domain.Entities;

/// <summary>
/// Represents a saved SQL Server database connection that can be reused across multiple data sources.
/// </summary>
public class DatabaseConnection : AuditableEntity
{
    public Guid Id { get; set; }

    /// <summary>
    /// User-friendly name for the connection (e.g., "Production HR Database")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of what this connection is used for
    /// </summary>
    public string? Description { get; set; }

    // Connection Details

    /// <summary>
    /// Server hostname or IP address (e.g., "192.168.50.112" or "sqlserver.domain.com")
    /// </summary>
    public string ServerHost { get; set; } = string.Empty;

    /// <summary>
    /// SQL Server port (default 1433)
    /// </summary>
    public int Port { get; set; } = 1433;

    /// <summary>
    /// Database/catalog name
    /// </summary>
    public string DatabaseName { get; set; } = string.Empty;

    // Authentication

    /// <summary>
    /// If true, use Windows Integrated Security instead of SQL authentication
    /// </summary>
    public bool UseWindowsAuth { get; set; } = false;

    /// <summary>
    /// SQL Server username (only used if UseWindowsAuth is false)
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Encrypted password (only used if UseWindowsAuth is false)
    /// </summary>
    public string? EncryptedPassword { get; set; }

    // Connection Options

    /// <summary>
    /// Trust the server certificate without validation (useful for self-signed certs)
    /// </summary>
    public bool TrustServerCertificate { get; set; } = true;

    /// <summary>
    /// Connection timeout in seconds
    /// </summary>
    public int ConnectionTimeout { get; set; } = 30;

    // Test Status

    /// <summary>
    /// When the connection was last tested
    /// </summary>
    public DateTime? LastTestedAt { get; set; }

    /// <summary>
    /// Result of the last connection test
    /// </summary>
    public bool? LastTestSuccessful { get; set; }

    // Navigation Properties

    /// <summary>
    /// SQL View data sources that use this connection
    /// </summary>
    public virtual ICollection<SqlViewDataSource> SqlViewDataSources { get; set; } = new List<SqlViewDataSource>();
}
