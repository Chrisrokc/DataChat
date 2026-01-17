using DataChat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataChat.Infrastructure.Persistence.Configurations;

public class BrandingConfigurationConfiguration : IEntityTypeConfiguration<BrandingConfiguration>
{
    public void Configure(EntityTypeBuilder<BrandingConfiguration> builder)
    {
        builder.ToTable("BrandingConfiguration", t =>
            t.HasCheckConstraint("CK_BrandingConfiguration_SingleRow", "[Id] = 1"));

        builder.HasKey(b => b.Id);

        builder.Property(b => b.ApplicationName)
            .HasMaxLength(100)
            .HasDefaultValue("Enterprise Chat");

        builder.Property(b => b.LogoPath)
            .HasMaxLength(500);

        builder.Property(b => b.PrimaryColor)
            .HasMaxLength(7)
            .HasDefaultValue("#1976D2");

        builder.Property(b => b.SecondaryColor)
            .HasMaxLength(7)
            .HasDefaultValue("#424242");

        builder.Property(b => b.AccentColor)
            .HasMaxLength(7)
            .HasDefaultValue("#FF4081");

        builder.Property(b => b.FooterText)
            .HasMaxLength(500);

        builder.Property(b => b.UpdatedBy)
            .HasMaxLength(256);

        // Seed default branding
        builder.HasData(new BrandingConfiguration
        {
            Id = 1,
            ApplicationName = "Enterprise Chat",
            PrimaryColor = "#1976D2",
            SecondaryColor = "#424242",
            AccentColor = "#FF4081"
        });
    }
}

public class SystemConfigurationConfiguration : IEntityTypeConfiguration<SystemConfiguration>
{
    public void Configure(EntityTypeBuilder<SystemConfiguration> builder)
    {
        builder.ToTable("SystemConfiguration", t =>
            t.HasCheckConstraint("CK_SystemConfiguration_SingleRow", "[Id] = 1"));

        builder.HasKey(s => s.Id);

        builder.Property(s => s.OpenAiApiKey)
            .HasMaxLength(500);

        builder.Property(s => s.OpenAiModel)
            .HasMaxLength(100)
            .HasDefaultValue("gpt-4o");

        builder.Property(s => s.EmbeddingModel)
            .HasMaxLength(100)
            .HasDefaultValue("text-embedding-ada-002");

        builder.Property(s => s.Temperature)
            .HasPrecision(3, 2)
            .HasDefaultValue(0.7m);

        builder.Property(s => s.UpdatedBy)
            .HasMaxLength(256);

        // Seed default configuration
        builder.HasData(new SystemConfiguration
        {
            Id = 1,
            OpenAiModel = "gpt-4o",
            EmbeddingModel = "text-embedding-ada-002",
            MaxTokensPerRequest = 4096,
            Temperature = 0.7m
        });
    }
}

public class SystemPromptConfiguration : IEntityTypeConfiguration<SystemPrompt>
{
    public void Configure(EntityTypeBuilder<SystemPrompt> builder)
    {
        builder.ToTable("SystemPrompts");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.PromptType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.Content)
            .IsRequired();

        builder.Property(p => p.CreatedBy)
            .HasMaxLength(256);

        builder.Property(p => p.UpdatedBy)
            .HasMaxLength(256);

        // Seed default prompts
        builder.HasData(
            new SystemPrompt
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Name = "Default Chat",
                PromptType = "DefaultChat",
                Content = "You are a helpful AI assistant. Provide clear, accurate, and helpful responses. Be concise but thorough in your explanations.",
                IsActive = true,
                Version = 1,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new SystemPrompt
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                Name = "Document RAG",
                PromptType = "DocumentRAG",
                Content = "You are a helpful AI assistant with access to organizational documents. Use the provided context to answer questions accurately. If the context doesn't contain relevant information, say so clearly. Always cite which document or source your information comes from when possible.",
                IsActive = true,
                Version = 1,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new SystemPrompt
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                Name = "SQL Query",
                PromptType = "SqlQuery",
                Content = "You are a SQL query assistant. Generate safe, read-only SQL SELECT queries based on natural language questions. Only generate SELECT statements - never INSERT, UPDATE, DELETE, or DDL statements. Explain your query logic.",
                IsActive = true,
                Version = 1,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.EntityType)
            .HasMaxLength(100);

        builder.Property(a => a.EntityId)
            .HasMaxLength(100);

        builder.Property(a => a.IpAddress)
            .HasMaxLength(45);

        builder.HasIndex(a => a.Timestamp);
        builder.HasIndex(a => a.UserId);
    }
}
