using DataChat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataChat.Infrastructure.Persistence.Configurations;

public class ChatFolderConfiguration : IEntityTypeConfiguration<ChatFolder>
{
    public void Configure(EntityTypeBuilder<ChatFolder> builder)
    {
        builder.ToTable("ChatFolders");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(f => f.Color)
            .HasMaxLength(20);

        builder.Property(f => f.CreatedBy)
            .HasMaxLength(256);

        builder.Property(f => f.UpdatedBy)
            .HasMaxLength(256);

        builder.HasIndex(f => new { f.UserId, f.SortOrder });

        builder.HasOne(f => f.User)
            .WithMany(u => u.ChatFolders)
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
