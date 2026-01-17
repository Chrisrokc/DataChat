using DataChat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataChat.Infrastructure.Persistence.Configurations;

public class DatabaseConnectionConfiguration : IEntityTypeConfiguration<DatabaseConnection>
{
    public void Configure(EntityTypeBuilder<DatabaseConnection> builder)
    {
        builder.ToTable("DatabaseConnections");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .HasMaxLength(256)
            .IsRequired();

        builder.HasIndex(c => c.Name)
            .IsUnique();

        builder.Property(c => c.Description)
            .HasMaxLength(500);

        builder.Property(c => c.ServerHost)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(c => c.DatabaseName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(c => c.Username)
            .HasMaxLength(256);

        builder.Property(c => c.EncryptedPassword)
            .HasMaxLength(1000);

        builder.Property(c => c.Port)
            .HasDefaultValue(1433);

        builder.Property(c => c.TrustServerCertificate)
            .HasDefaultValue(true);

        builder.Property(c => c.ConnectionTimeout)
            .HasDefaultValue(30);

        builder.Property(c => c.CreatedBy)
            .HasMaxLength(256);

        builder.Property(c => c.UpdatedBy)
            .HasMaxLength(256);

        // Relationship with SqlViewDataSource is configured in DataSourceConfiguration
    }
}
