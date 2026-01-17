namespace DataChat.Domain.Entities;

public class AdGroupDataSourcePermission
{
    public Guid Id { get; set; }
    public string AdGroupSid { get; set; } = string.Empty;
    public string AdGroupName { get; set; } = string.Empty;
    public Guid DataSourceId { get; set; }
    public bool CanRead { get; set; } = true;
    public DateTime GrantedAt { get; set; }
    public string? GrantedBy { get; set; }

    // Navigation properties
    public virtual DataSource DataSource { get; set; } = null!;
}
