using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataChat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentAccessSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DocumentAccessTokenExpirationMinutes",
                table: "SystemConfiguration",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "EnableDocumentDownload",
                table: "SystemConfiguration",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableDocumentPreview",
                table: "SystemConfiguration",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "SystemConfiguration",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "DocumentAccessTokenExpirationMinutes", "EnableDocumentDownload", "EnableDocumentPreview" },
                values: new object[] { 10, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentAccessTokenExpirationMinutes",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "EnableDocumentDownload",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "EnableDocumentPreview",
                table: "SystemConfiguration");
        }
    }
}
