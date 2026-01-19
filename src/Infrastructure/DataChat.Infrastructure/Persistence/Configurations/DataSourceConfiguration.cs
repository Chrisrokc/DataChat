using DataChat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataChat.Infrastructure.Persistence.Configurations;

public class DataSourceConfiguration : IEntityTypeConfiguration<DataSource>
{
    public void Configure(EntityTypeBuilder<DataSource> builder)
    {
        builder.ToTable("DataSources");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Name)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(d => d.Description)
            .HasMaxLength(1000);

        builder.Property(d => d.Type)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.CreatedBy)
            .HasMaxLength(256);

        builder.Property(d => d.UpdatedBy)
            .HasMaxLength(256);

        builder.HasOne(d => d.FileSystemDataSource)
            .WithOne(f => f.DataSource)
            .HasForeignKey<FileSystemDataSource>(f => f.DataSourceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.SqlViewDataSource)
            .WithOne(s => s.DataSource)
            .HasForeignKey<SqlViewDataSource>(s => s.DataSourceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Personal document ownership relationship
        builder.HasOne(d => d.Owner)
            .WithMany()
            .HasForeignKey(d => d.OwnerUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(d => d.OwnerUserId);
    }
}

public class FileSystemDataSourceConfiguration : IEntityTypeConfiguration<FileSystemDataSource>
{
    public void Configure(EntityTypeBuilder<FileSystemDataSource> builder)
    {
        builder.ToTable("FileSystemDataSources");

        builder.HasKey(f => f.DataSourceId);

        builder.Property(f => f.FolderPath)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(f => f.FilePatterns)
            .HasMaxLength(500);

        builder.Property(f => f.SyncStatus)
            .HasConversion<string>()
            .HasMaxLength(50);
    }
}

public class SqlViewDataSourceConfiguration : IEntityTypeConfiguration<SqlViewDataSource>
{
    public void Configure(EntityTypeBuilder<SqlViewDataSource> builder)
    {
        builder.ToTable("SqlViewDataSources");

        builder.HasKey(s => s.DataSourceId);

        // ConnectionString is now nullable for new records that use DatabaseConnection
        builder.Property(s => s.ConnectionString)
            .HasMaxLength(1000);

        builder.Property(s => s.ViewName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(s => s.SchemaName)
            .HasMaxLength(128)
            .HasDefaultValue("dbo");

        // Sync status tracking
        builder.Property(s => s.SyncStatus)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(s => s.LastSyncError)
            .HasMaxLength(2000);

        // Relationship with DatabaseConnection
        builder.HasOne(s => s.DatabaseConnection)
            .WithMany(c => c.SqlViewDataSources)
            .HasForeignKey(s => s.DatabaseConnectionId)
            .OnDelete(DeleteBehavior.Restrict); // Prevent deletion of connection if data sources use it
    }
}
