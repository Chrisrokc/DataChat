using DataChat.Domain.Common;

namespace DataChat.Domain.Entities;

/// <summary>
/// Stores persistent memory/preferences for a user that the AI will remember across all chat sessions.
/// </summary>
public class UserMemory : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>
    /// Category of the memory (e.g., "preference", "fact", "instruction", "context")
    /// </summary>
    public string Category { get; set; } = "general";

    /// <summary>
    /// The actual memory content that will be injected into AI context
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Whether this memory is currently active and should be included in AI context
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional priority for ordering memories (higher = more important)
    /// </summary>
    public int Priority { get; set; } = 0;

    // Navigation property
    public virtual User? User { get; set; }
}
