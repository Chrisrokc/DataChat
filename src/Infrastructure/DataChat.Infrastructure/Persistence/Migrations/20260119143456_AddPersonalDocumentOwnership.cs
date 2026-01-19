using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataChat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonalDocumentOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "DataSources",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DataSources_OwnerUserId",
                table: "DataSources",
                column: "OwnerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_DataSources_Users_OwnerUserId",
                table: "DataSources",
                column: "OwnerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DataSources_Users_OwnerUserId",
                table: "DataSources");

            migrationBuilder.DropIndex(
                name: "IX_DataSources_OwnerUserId",
                table: "DataSources");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "DataSources");
        }
    }
}
