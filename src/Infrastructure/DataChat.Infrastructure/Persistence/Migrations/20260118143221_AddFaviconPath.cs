using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataChat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFaviconPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FaviconPath",
                table: "BrandingConfiguration",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "BrandingConfiguration",
                keyColumn: "Id",
                keyValue: 1,
                column: "FaviconPath",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FaviconPath",
                table: "BrandingConfiguration");
        }
    }
}
