using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataChat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthenticationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "OllamaModel",
                table: "SystemConfiguration",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldDefaultValue: "llama3.2");

            migrationBuilder.AlterColumn<string>(
                name: "OllamaEndpoint",
                table: "SystemConfiguration",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldDefaultValue: "http://localhost:11434");

            migrationBuilder.AlterColumn<string>(
                name: "OllamaEmbeddingModel",
                table: "SystemConfiguration",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldDefaultValue: "nomic-embed-text");

            migrationBuilder.AlterColumn<string>(
                name: "AzureOpenAiEndpoint",
                table: "SystemConfiguration",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AzureOpenAiEmbeddingDeployment",
                table: "SystemConfiguration",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AzureOpenAiDeploymentName",
                table: "SystemConfiguration",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AzureOpenAiApiVersion",
                table: "SystemConfiguration",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldDefaultValue: "2024-02-15-preview");

            migrationBuilder.AddColumn<string>(
                name: "AuthenticationMode",
                table: "SystemConfiguration",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "WindowsAuthAutoProvisionUsers",
                table: "SystemConfiguration",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "WindowsAuthDefaultRole",
                table: "SystemConfiguration",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "SystemConfiguration",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "AuthenticationMode", "WindowsAuthAutoProvisionUsers", "WindowsAuthDefaultRole" },
                values: new object[] { "Local", true, "User" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthenticationMode",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "WindowsAuthAutoProvisionUsers",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "WindowsAuthDefaultRole",
                table: "SystemConfiguration");

            migrationBuilder.AlterColumn<string>(
                name: "OllamaModel",
                table: "SystemConfiguration",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "llama3.2",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "OllamaEndpoint",
                table: "SystemConfiguration",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "http://localhost:11434",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "OllamaEmbeddingModel",
                table: "SystemConfiguration",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "nomic-embed-text",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "AzureOpenAiEndpoint",
                table: "SystemConfiguration",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AzureOpenAiEmbeddingDeployment",
                table: "SystemConfiguration",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AzureOpenAiDeploymentName",
                table: "SystemConfiguration",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AzureOpenAiApiVersion",
                table: "SystemConfiguration",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "2024-02-15-preview",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }
    }
}
