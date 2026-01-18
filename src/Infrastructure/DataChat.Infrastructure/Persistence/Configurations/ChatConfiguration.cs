using DataChat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataChat.Infrastructure.Persistence.Configurations;

public class ChatConfiguration : IEntityTypeConfiguration<Chat>
{
    public void Configure(EntityTypeBuilder<Chat> builder)
    {
        builder.ToTable("Chats");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Title)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(c => c.CreatedBy)
            .HasMaxLength(256);

        builder.Property(c => c.UpdatedBy)
            .HasMaxLength(256);

        builder.HasIndex(c => c.UserId);
        builder.HasIndex(c => c.FolderId);
        builder.HasIndex(c => new { c.UserId, c.IsPinned });

        builder.HasQueryFilter(c => !c.IsDeleted);

        builder.HasOne(c => c.Folder)
            .WithMany(f => f.Chats)
            .HasForeignKey(c => c.FolderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(c => c.Messages)
            .WithOne(m => m.Chat)
            .HasForeignKey(m => m.ChatId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("ChatMessages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(m => m.Content)
            .IsRequired();

        builder.HasIndex(m => m.ChatId);
        builder.HasIndex(m => m.CreatedAt);
    }
}
