using DataChat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataChat.Infrastructure.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(r => r.Name)
            .IsUnique();

        builder.Property(r => r.Description)
            .HasMaxLength(256);

        // Seed default roles
        builder.HasData(
            new Role { Id = 1, Name = "User", Description = "Standard user with chat access", CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Role { Id = 2, Name = "Admin", Description = "Administrator with full access", CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRoles");

        builder.HasKey(ur => new { ur.UserId, ur.RoleId });

        builder.Property(ur => ur.AssignedBy)
            .HasMaxLength(256);

        builder.HasOne(ur => ur.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ur => ur.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(ur => ur.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Assign admin role to default admin user
        builder.HasData(new UserRole
        {
            UserId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            RoleId = 2, // Admin role
            AssignedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            AssignedBy = "System"
        });
    }
}

public class AdGroupRoleMappingConfiguration : IEntityTypeConfiguration<AdGroupRoleMapping>
{
    public void Configure(EntityTypeBuilder<AdGroupRoleMapping> builder)
    {
        builder.ToTable("AdGroupRoleMappings");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.AdGroupSid)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(m => m.AdGroupName)
            .HasMaxLength(256)
            .IsRequired();

        builder.HasIndex(m => new { m.AdGroupSid, m.RoleId })
            .IsUnique();

        builder.HasOne(m => m.Role)
            .WithMany(r => r.AdGroupMappings)
            .HasForeignKey(m => m.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
