using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataChat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSourcePreviewFilterSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourcePreviewMaxSources",
                table: "SystemConfiguration",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SourcePreviewMinRelevance",
                table: "SystemConfiguration",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "SystemConfiguration",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "SourcePreviewMaxSources", "SourcePreviewMinRelevance" },
                values: new object[] { 5, 0 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourcePreviewMaxSources",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "SourcePreviewMinRelevance",
                table: "SystemConfiguration");
        }
    }
}
