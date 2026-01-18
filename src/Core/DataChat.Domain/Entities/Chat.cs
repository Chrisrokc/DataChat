using DataChat.Domain.Common;

namespace DataChat.Domain.Entities;

public class Chat : AuditableEntity, ISoftDelete
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? FolderId { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual ChatFolder? Folder { get; set; }
    public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
