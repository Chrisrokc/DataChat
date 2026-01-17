using DataChat.Domain.Common;
using DataChat.Domain.Enums;

namespace DataChat.Domain.Entities;

public class DataSource : AuditableEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DataSourceType Type { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual FileSystemDataSource? FileSystemDataSource { get; set; }
    public virtual SqlViewDataSource? SqlViewDataSource { get; set; }
    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
    public virtual ICollection<UserDataSourcePermission> UserPermissions { get; set; } = new List<UserDataSourcePermission>();
    public virtual ICollection<AdGroupDataSourcePermission> AdGroupPermissions { get; set; } = new List<AdGroupDataSourcePermission>();
}
