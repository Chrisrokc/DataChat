using DataChat.Domain.Enums;

namespace DataChat.Domain.Entities;

public class ChatMessage
{
    public Guid Id { get; set; }
    public Guid ChatId { get; set; }
    public MessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public int? TokenCount { get; set; }
    public int? InputTokens { get; set; }  // For cost tracking
    public int? OutputTokens { get; set; } // For cost tracking
    public string? DataSourcesUsed { get; set; } // JSON array of data source IDs
    public string? SourceChunksJson { get; set; } // JSON array of RAG source chunks used (for source preview feature)
    public string? AttachmentsJson { get; set; } // JSON array of file/image attachments
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public virtual Chat Chat { get; set; } = null!;
    public virtual ICollection<MessageReaction> Reactions { get; set; } = new List<MessageReaction>();
}
