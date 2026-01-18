using DataChat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataChat.Infrastructure.Persistence.Configurations;

public class UserMemoryConfiguration : IEntityTypeConfiguration<UserMemory>
{
    public void Configure(EntityTypeBuilder<UserMemory> builder)
    {
        builder.ToTable("UserMemories");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Category)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(m => m.Content)
            .IsRequired();

        builder.Property(m => m.CreatedBy)
            .HasMaxLength(256);

        builder.Property(m => m.UpdatedBy)
            .HasMaxLength(256);

        builder.HasIndex(m => new { m.UserId, m.IsActive });

        builder.HasOne(m => m.User)
            .WithMany(u => u.Memories)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
