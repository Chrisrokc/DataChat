using DataChat.Domain.Common;
using DataChat.Domain.Enums;

namespace DataChat.Domain.Entities;

public class Document : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid DataSourceId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty; // SHA256 for change detection
    public long FileSize { get; set; }
    public string? MimeType { get; set; }
    public DocumentStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? ProcessedAt { get; set; }

    // Navigation properties
    public virtual DataSource DataSource { get; set; } = null!;
    public virtual ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
}
