using DataChat.Domain.Enums;

namespace DataChat.Domain.Entities;

public class SqlViewDataSource
{
    public Guid DataSourceId { get; set; }

    /// <summary>
    /// Reference to a saved database connection (preferred method).
    /// If set, ConnectionString is ignored and built from the DatabaseConnection.
    /// </summary>
    public Guid? DatabaseConnectionId { get; set; }

    /// <summary>
    /// Legacy connection string (encrypted). Used for backward compatibility.
    /// New data sources should use DatabaseConnectionId instead.
    /// </summary>
    public string? ConnectionString { get; set; } // Encrypted, nullable for new records

    public string ViewName { get; set; } = string.Empty;
    public string SchemaName { get; set; } = "dbo";
    public string? ViewDescription { get; set; } // AI uses this to understand the view
    public string? ColumnMetadata { get; set; } // JSON describing columns for AI
    public int MaxRowsReturned { get; set; } = 1000;
    public bool AllowAggregations { get; set; } = true;
    public DateTime? LastValidatedAt { get; set; }

    // Sync tracking (same as FileSystemDataSource)
    public SyncStatus? SyncStatus { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public int? LastSyncRowCount { get; set; }
    public string? LastSyncError { get; set; }

    // Navigation properties
    public virtual DataSource DataSource { get; set; } = null!;
    public virtual DatabaseConnection? DatabaseConnection { get; set; }
}
