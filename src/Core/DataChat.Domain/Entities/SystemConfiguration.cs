namespace DataChat.Domain.Entities;

public class SystemConfiguration
{
    public int Id { get; set; } = 1; // Single row

    // AI Assistant Branding
    public string AiAssistantName { get; set; } = "Assistant";

    // OpenAI Settings
    public string? OpenAiApiKey { get; set; } // Encrypted
    public string OpenAiModel { get; set; } = "gpt-4o";
    public string EmbeddingModel { get; set; } = "text-embedding-ada-002";
    public int MaxTokensPerRequest { get; set; } = 4096;
    public decimal Temperature { get; set; } = 0.7m;

    // SQL Server 2025 Connection Settings (for queryable views)
    public string? SqlServerHost { get; set; }
    public int SqlServerPort { get; set; } = 1433;
    public string? SqlServerDatabase { get; set; }
    public string? SqlServerUsername { get; set; }
    public string? SqlServerPassword { get; set; } // Encrypted
    public bool SqlServerUseIntegratedSecurity { get; set; } = false;
    public bool SqlServerTrustServerCertificate { get; set; } = true;
    public int SqlServerConnectionTimeout { get; set; } = 30;

    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
