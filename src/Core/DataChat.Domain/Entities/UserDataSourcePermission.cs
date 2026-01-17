namespace DataChat.Domain.Entities;

public class UserDataSourcePermission
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid DataSourceId { get; set; }
    public bool CanRead { get; set; } = true;
    public DateTime GrantedAt { get; set; }
    public string? GrantedBy { get; set; }

    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual DataSource DataSource { get; set; } = null!;
}
