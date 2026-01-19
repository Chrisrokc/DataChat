using DataChat.Domain.Enums;

namespace DataChat.Domain.Entities;

public class SystemConfiguration
{
    public int Id { get; set; } = 1; // Single row

    // AI Assistant Branding
    public string AiAssistantName { get; set; } = "Assistant";

    // LLM Provider Selection
    public LlmProvider LlmProvider { get; set; } = LlmProvider.OpenAI;

    // OpenAI Settings
    public string? OpenAiApiKey { get; set; } // Encrypted
    public string OpenAiModel { get; set; } = "gpt-4o";
    public string EmbeddingModel { get; set; } = "text-embedding-ada-002";
    public int MaxTokensPerRequest { get; set; } = 4096;
    public decimal Temperature { get; set; } = 0.7m;

    // Azure OpenAI Settings
    public string? AzureOpenAiEndpoint { get; set; } // e.g., https://myinstance.openai.azure.com
    public string? AzureOpenAiApiKey { get; set; } // Encrypted
    public string? AzureOpenAiDeploymentName { get; set; } // Chat deployment name
    public string? AzureOpenAiEmbeddingDeployment { get; set; } // Embedding deployment name
    public string AzureOpenAiApiVersion { get; set; } = "2024-02-15-preview";

    // Ollama Settings
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "llama3.2";
    public string OllamaEmbeddingModel { get; set; } = "nomic-embed-text";

    // SQL Server 2025 Connection Settings (for queryable views)
    public string? SqlServerHost { get; set; }
    public int SqlServerPort { get; set; } = 1433;
    public string? SqlServerDatabase { get; set; }
    public string? SqlServerUsername { get; set; }
    public string? SqlServerPassword { get; set; } // Encrypted
    public bool SqlServerUseIntegratedSecurity { get; set; } = false;
    public bool SqlServerTrustServerCertificate { get; set; } = true;
    public int SqlServerConnectionTimeout { get; set; } = 30;

    // Announcement Banner Settings
    public bool AnnouncementEnabled { get; set; } = false;
    public string? AnnouncementMessage { get; set; }
    public string AnnouncementType { get; set; } = "info"; // info, warning, error, success
    public bool AnnouncementDismissible { get; set; } = true;
    public DateTime? AnnouncementStartDate { get; set; }
    public DateTime? AnnouncementEndDate { get; set; }

    // Cost Tracking Settings
    public decimal CostPerInputToken { get; set; } = 0.00001m; // Default for GPT-4o
    public decimal CostPerOutputToken { get; set; } = 0.00003m; // Default for GPT-4o
    public decimal MonthlyCostBudget { get; set; } = 0; // 0 = no limit
    public bool CostAlertEnabled { get; set; } = false;
    public decimal CostAlertThreshold { get; set; } = 80; // Percentage of budget

    // RAG Settings
    public bool EnableSourcePreview { get; set; } = true; // Allow users to preview document chunks used in RAG responses
    public bool EnableDocumentPreview { get; set; } = true; // Allow in-browser document viewing
    public bool EnableDocumentDownload { get; set; } = true; // Allow document download
    public int DocumentAccessTokenExpirationMinutes { get; set; } = 10; // Token validity period (1-60 minutes)
    public int SourcePreviewMinRelevance { get; set; } = 0; // Minimum relevance % to show in source preview (0-100, 0 = show all)
    public int SourcePreviewMaxSources { get; set; } = 5; // Maximum number of sources to display (1-10)

    // Authentication Settings
    public string AuthenticationMode { get; set; } = "Local"; // "Local" or "Windows"
    public bool WindowsAuthAutoProvisionUsers { get; set; } = true; // Auto-create users on first Windows auth login
    public string WindowsAuthDefaultRole { get; set; } = "User"; // Default role for auto-provisioned users
    public string? WindowsAuthAllowedDomains { get; set; } // Semicolon-delimited, e.g., "CORP;PARTNERS" (empty = allow all)

    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
