using DataChat.Domain.Enums;

namespace DataChat.Domain.Entities;

public class FileSystemDataSource
{
    public Guid DataSourceId { get; set; }
    public string FolderPath { get; set; } = string.Empty;
    public string? FilePatterns { get; set; } // e.g., "*.pdf;*.docx"
    public bool IncludeSubfolders { get; set; } = true;
    public DateTime? LastSyncAt { get; set; }
    public SyncStatus? SyncStatus { get; set; }

    // Navigation properties
    public virtual DataSource DataSource { get; set; } = null!;
}
