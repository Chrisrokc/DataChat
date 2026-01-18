using DataChat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataChat.Infrastructure.Persistence.Configurations;

public class MessageReactionConfiguration : IEntityTypeConfiguration<MessageReaction>
{
    public void Configure(EntityTypeBuilder<MessageReaction> builder)
    {
        builder.ToTable("MessageReactions");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.ReactionType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.FeedbackText)
            .HasMaxLength(2000);

        builder.Property(r => r.FeedbackCategory)
            .HasMaxLength(50);

        builder.Property(r => r.CreatedBy)
            .HasMaxLength(256);

        builder.Property(r => r.UpdatedBy)
            .HasMaxLength(256);

        // Indexes for analytics queries
        builder.HasIndex(r => r.MessageId);
        builder.HasIndex(r => r.UserId);
        builder.HasIndex(r => r.ReactionType);
        builder.HasIndex(r => r.CreatedAt);
        builder.HasIndex(r => new { r.MessageId, r.UserId }).IsUnique(); // One reaction per user per message

        builder.HasOne(r => r.Message)
            .WithMany(m => m.Reactions)
            .HasForeignKey(r => r.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
