using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataChat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDatabaseConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ConnectionString",
                table: "SqlViewDataSources",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AddColumn<Guid>(
                name: "DatabaseConnectionId",
                table: "SqlViewDataSources",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DatabaseConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ServerHost = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Port = table.Column<int>(type: "int", nullable: false, defaultValue: 1433),
                    DatabaseName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UseWindowsAuth = table.Column<bool>(type: "bit", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EncryptedPassword = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TrustServerCertificate = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ConnectionTimeout = table.Column<int>(type: "int", nullable: false, defaultValue: 30),
                    LastTestedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastTestSuccessful = table.Column<bool>(type: "bit", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatabaseConnections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SqlViewDataSources_DatabaseConnectionId",
                table: "SqlViewDataSources",
                column: "DatabaseConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_DatabaseConnections_Name",
                table: "DatabaseConnections",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SqlViewDataSources_DatabaseConnections_DatabaseConnectionId",
                table: "SqlViewDataSources",
                column: "DatabaseConnectionId",
                principalTable: "DatabaseConnections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SqlViewDataSources_DatabaseConnections_DatabaseConnectionId",
                table: "SqlViewDataSources");

            migrationBuilder.DropTable(
                name: "DatabaseConnections");

            migrationBuilder.DropIndex(
                name: "IX_SqlViewDataSources_DatabaseConnectionId",
                table: "SqlViewDataSources");

            migrationBuilder.DropColumn(
                name: "DatabaseConnectionId",
                table: "SqlViewDataSources");

            migrationBuilder.AlterColumn<string>(
                name: "ConnectionString",
                table: "SqlViewDataSources",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);
        }
    }
}
