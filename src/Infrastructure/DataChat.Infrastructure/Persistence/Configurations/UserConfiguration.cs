using DataChat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataChat.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Username)
            .HasMaxLength(256)
            .IsRequired();

        builder.HasIndex(u => u.Username)
            .IsUnique();

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(256);

        builder.Property(u => u.WindowsIdentity)
            .HasMaxLength(256);

        builder.HasIndex(u => u.WindowsIdentity)
            .IsUnique()
            .HasFilter("[WindowsIdentity] IS NOT NULL");

        builder.Property(u => u.DisplayName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(u => u.Email)
            .HasMaxLength(256);

        builder.Property(u => u.CreatedBy)
            .HasMaxLength(256);

        builder.Property(u => u.UpdatedBy)
            .HasMaxLength(256);

        // Seed default admin user (password: admin123)
        builder.HasData(new User
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Username = "admin",
            PasswordHash = "AQAAAAIAAYagAAAAEK8+J8Zf2K+1Z5cL1l5XH5Q5Z5Q5Z5Q5Z5Q5Z5Q5Z5Q5Z5Q5Z5Q5Z5Q5Z5Q5Z5Q5Zw==", // Placeholder - will set properly
            DisplayName = "Administrator",
            Email = "admin@enterprise.local",
            IsActive = true,
            CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        builder.HasMany(u => u.Chats)
            .WithOne(c => c.User)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.DataSourcePermissions)
            .WithOne(p => p.User)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
