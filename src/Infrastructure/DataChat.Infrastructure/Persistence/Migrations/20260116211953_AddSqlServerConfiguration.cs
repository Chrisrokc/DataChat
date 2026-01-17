using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataChat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSqlServerConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SqlServerConnectionTimeout",
                table: "SystemConfiguration",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SqlServerDatabase",
                table: "SystemConfiguration",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SqlServerHost",
                table: "SystemConfiguration",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SqlServerPassword",
                table: "SystemConfiguration",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SqlServerPort",
                table: "SystemConfiguration",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "SqlServerTrustServerCertificate",
                table: "SystemConfiguration",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SqlServerUseIntegratedSecurity",
                table: "SystemConfiguration",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SqlServerUsername",
                table: "SystemConfiguration",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "SystemConfiguration",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "SqlServerConnectionTimeout", "SqlServerDatabase", "SqlServerHost", "SqlServerPassword", "SqlServerPort", "SqlServerTrustServerCertificate", "SqlServerUseIntegratedSecurity", "SqlServerUsername" },
                values: new object[] { 30, null, null, null, 1433, true, false, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SqlServerConnectionTimeout",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "SqlServerDatabase",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "SqlServerHost",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "SqlServerPassword",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "SqlServerPort",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "SqlServerTrustServerCertificate",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "SqlServerUseIntegratedSecurity",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "SqlServerUsername",
                table: "SystemConfiguration");
        }
    }
}
