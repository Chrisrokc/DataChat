using DataChat.Domain.Enums;

namespace DataChat.Domain.Entities;

public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public virtual ICollection<AdGroupRoleMapping> AdGroupMappings { get; set; } = new List<AdGroupRoleMapping>();
}
