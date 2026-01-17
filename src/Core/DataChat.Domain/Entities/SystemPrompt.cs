using DataChat.Domain.Common;

namespace DataChat.Domain.Entities;

public class SystemPrompt : AuditableEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PromptType { get; set; } = string.Empty; // 'DefaultChat', 'DocumentRAG', 'SqlQuery'
    public string Content { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;
}
