namespace DataChat.Domain.Entities;

public class AdGroupRoleMapping
{
    public int Id { get; set; }
    public string AdGroupSid { get; set; } = string.Empty;
    public string AdGroupName { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public virtual Role Role { get; set; } = null!;
}
