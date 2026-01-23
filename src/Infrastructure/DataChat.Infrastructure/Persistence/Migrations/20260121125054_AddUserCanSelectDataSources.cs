using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataChat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserCanSelectDataSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Check if column already exists before adding
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Users') AND name = 'CanSelectDataSources')
                BEGIN
                    ALTER TABLE [Users] ADD [CanSelectDataSources] bit NOT NULL DEFAULT 0;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanSelectDataSources",
                table: "Users");
        }
    }
}
