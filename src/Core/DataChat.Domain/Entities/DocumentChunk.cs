namespace DataChat.Domain.Entities;

public class DocumentChunk
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public int? TokenCount { get; set; }
    public float[]? Embedding { get; set; } // Vector embedding (1536 dimensions for OpenAI ada-002)
    public string? Metadata { get; set; } // JSON for page numbers, headings, etc.
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public virtual Document Document { get; set; } = null!;
}
