using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataChat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSqlViewSyncStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncAt",
                table: "SqlViewDataSources",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastSyncError",
                table: "SqlViewDataSources",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastSyncRowCount",
                table: "SqlViewDataSources",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SyncStatus",
                table: "SqlViewDataSources",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastSyncAt",
                table: "SqlViewDataSources");

            migrationBuilder.DropColumn(
                name: "LastSyncError",
                table: "SqlViewDataSources");

            migrationBuilder.DropColumn(
                name: "LastSyncRowCount",
                table: "SqlViewDataSources");

            migrationBuilder.DropColumn(
                name: "SyncStatus",
                table: "SqlViewDataSources");
        }
    }
}
