using DataChat.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataChat.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<AdGroupRoleMapping> AdGroupRoleMappings { get; }
    DbSet<Chat> Chats { get; }
    DbSet<ChatMessage> ChatMessages { get; }
    DbSet<DataSource> DataSources { get; }
    DbSet<FileSystemDataSource> FileSystemDataSources { get; }
    DbSet<SqlViewDataSource> SqlViewDataSources { get; }
    DbSet<DatabaseConnection> DatabaseConnections { get; }
    DbSet<Document> Documents { get; }
    DbSet<DocumentChunk> DocumentChunks { get; }
    DbSet<UserDataSourcePermission> UserDataSourcePermissions { get; }
    DbSet<AdGroupDataSourcePermission> AdGroupDataSourcePermissions { get; }
    DbSet<BrandingConfiguration> BrandingConfiguration { get; }
    DbSet<SystemConfiguration> SystemConfiguration { get; }
    DbSet<SystemPrompt> SystemPrompts { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
