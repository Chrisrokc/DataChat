namespace DataChat.Domain.Models;

/// <summary>
/// Represents a file or image attachment in a chat message.
/// Stored as JSON in ChatMessage.AttachmentsJson.
/// </summary>
public class MessageAttachment
{
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public string Base64Data { get; set; } = string.Empty;
    public long FileSize { get; set; }
}
