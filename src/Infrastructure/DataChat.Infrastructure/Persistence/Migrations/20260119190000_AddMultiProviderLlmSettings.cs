using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataChat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiProviderLlmSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // LLM Provider Selection
            migrationBuilder.AddColumn<int>(
                name: "LlmProvider",
                table: "SystemConfiguration",
                type: "int",
                nullable: false,
                defaultValue: 0); // OpenAI

            // Azure OpenAI Settings
            migrationBuilder.AddColumn<string>(
                name: "AzureOpenAiEndpoint",
                table: "SystemConfiguration",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AzureOpenAiApiKey",
                table: "SystemConfiguration",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AzureOpenAiDeploymentName",
                table: "SystemConfiguration",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AzureOpenAiEmbeddingDeployment",
                table: "SystemConfiguration",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AzureOpenAiApiVersion",
                table: "SystemConfiguration",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "2024-02-15-preview");

            // Ollama Settings
            migrationBuilder.AddColumn<string>(
                name: "OllamaEndpoint",
                table: "SystemConfiguration",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "http://localhost:11434");

            migrationBuilder.AddColumn<string>(
                name: "OllamaModel",
                table: "SystemConfiguration",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "llama3.2");

            migrationBuilder.AddColumn<string>(
                name: "OllamaEmbeddingModel",
                table: "SystemConfiguration",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "nomic-embed-text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LlmProvider",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "AzureOpenAiEndpoint",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "AzureOpenAiApiKey",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "AzureOpenAiDeploymentName",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "AzureOpenAiEmbeddingDeployment",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "AzureOpenAiApiVersion",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "OllamaEndpoint",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "OllamaModel",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "OllamaEmbeddingModel",
                table: "SystemConfiguration");
        }
    }
}
