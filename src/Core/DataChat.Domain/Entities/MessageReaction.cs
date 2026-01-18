using DataChat.Domain.Common;

namespace DataChat.Domain.Entities;

/// <summary>
/// Stores user feedback/reactions on AI-generated messages for quality tracking and analytics.
/// </summary>
public class MessageReaction : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>
    /// Reaction type: "thumbs_up", "thumbs_down", "helpful", "not_helpful", etc.
    /// </summary>
    public string ReactionType { get; set; } = string.Empty;

    /// <summary>
    /// Optional feedback text provided by the user
    /// </summary>
    public string? FeedbackText { get; set; }

    /// <summary>
    /// Optional category for negative feedback (e.g., "incorrect", "incomplete", "harmful", "off_topic")
    /// </summary>
    public string? FeedbackCategory { get; set; }

    // Navigation properties
    public virtual ChatMessage? Message { get; set; }
    public virtual User? User { get; set; }
}
