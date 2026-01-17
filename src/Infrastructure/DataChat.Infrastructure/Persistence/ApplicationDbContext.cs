using DataChat.Application.Common.Interfaces;
using DataChat.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataChat.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<AdGroupRoleMapping> AdGroupRoleMappings => Set<AdGroupRoleMapping>();
    public DbSet<Chat> Chats => Set<Chat>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<DataSource> DataSources => Set<DataSource>();
    public DbSet<FileSystemDataSource> FileSystemDataSources => Set<FileSystemDataSource>();
    public DbSet<SqlViewDataSource> SqlViewDataSources => Set<SqlViewDataSource>();
    public DbSet<DatabaseConnection> DatabaseConnections => Set<DatabaseConnection>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<UserDataSourcePermission> UserDataSourcePermissions => Set<UserDataSourcePermission>();
    public DbSet<AdGroupDataSourcePermission> AdGroupDataSourcePermissions => Set<AdGroupDataSourcePermission>();
    public DbSet<BrandingConfiguration> BrandingConfiguration => Set<BrandingConfiguration>();
    public DbSet<SystemConfiguration> SystemConfiguration => Set<SystemConfiguration>();
    public DbSet<SystemPrompt> SystemPrompts => Set<SystemPrompt>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
