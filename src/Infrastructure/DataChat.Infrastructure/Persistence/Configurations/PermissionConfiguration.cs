using DataChat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataChat.Infrastructure.Persistence.Configurations;

public class UserDataSourcePermissionConfiguration : IEntityTypeConfiguration<UserDataSourcePermission>
{
    public void Configure(EntityTypeBuilder<UserDataSourcePermission> builder)
    {
        builder.ToTable("UserDataSourcePermissions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.GrantedBy)
            .HasMaxLength(256);

        builder.HasIndex(p => new { p.UserId, p.DataSourceId })
            .IsUnique();

        builder.HasOne(p => p.DataSource)
            .WithMany(d => d.UserPermissions)
            .HasForeignKey(p => p.DataSourceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AdGroupDataSourcePermissionConfiguration : IEntityTypeConfiguration<AdGroupDataSourcePermission>
{
    public void Configure(EntityTypeBuilder<AdGroupDataSourcePermission> builder)
    {
        builder.ToTable("AdGroupDataSourcePermissions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.AdGroupSid)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(p => p.AdGroupName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(p => p.GrantedBy)
            .HasMaxLength(256);

        builder.HasIndex(p => new { p.AdGroupSid, p.DataSourceId })
            .IsUnique();

        builder.HasOne(p => p.DataSource)
            .WithMany(d => d.AdGroupPermissions)
            .HasForeignKey(p => p.DataSourceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
