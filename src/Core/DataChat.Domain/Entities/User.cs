using DataChat.Domain.Common;

namespace DataChat.Domain.Entities;

public class User : AuditableEntity
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? PasswordHash { get; set; } // Null for Windows Auth users
    public string? WindowsIdentity { get; set; } // Null for local auth users
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public bool CanSelectDataSources { get; set; } = false; // Allow user to select data sources in chat (admin feature)

    // Navigation properties
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public virtual ICollection<Chat> Chats { get; set; } = new List<Chat>();
    public virtual ICollection<ChatFolder> ChatFolders { get; set; } = new List<ChatFolder>();
    public virtual ICollection<UserDataSourcePermission> DataSourcePermissions { get; set; } = new List<UserDataSourcePermission>();
    public virtual ICollection<UserMemory> Memories { get; set; } = new List<UserMemory>();
}
