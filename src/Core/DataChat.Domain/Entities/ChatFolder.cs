using DataChat.Domain.Common;

namespace DataChat.Domain.Entities;

/// <summary>
/// Represents a folder for organizing chat conversations.
/// </summary>
public class ChatFolder : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>
    /// Display name of the folder
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional color for the folder (hex code)
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Display order for sorting folders
    /// </summary>
    public int SortOrder { get; set; } = 0;

    // Navigation properties
    public virtual User? User { get; set; }
    public virtual ICollection<Chat> Chats { get; set; } = new List<Chat>();
}
