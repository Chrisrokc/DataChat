using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataChat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChatFolders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FolderId",
                table: "Chats",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "Chats",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ChatFolders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatFolders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatFolders_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Chats_FolderId",
                table: "Chats",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_Chats_UserId_IsPinned",
                table: "Chats",
                columns: new[] { "UserId", "IsPinned" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatFolders_UserId_SortOrder",
                table: "ChatFolders",
                columns: new[] { "UserId", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_Chats_ChatFolders_FolderId",
                table: "Chats",
                column: "FolderId",
                principalTable: "ChatFolders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Chats_ChatFolders_FolderId",
                table: "Chats");

            migrationBuilder.DropTable(
                name: "ChatFolders");

            migrationBuilder.DropIndex(
                name: "IX_Chats_FolderId",
                table: "Chats");

            migrationBuilder.DropIndex(
                name: "IX_Chats_UserId_IsPinned",
                table: "Chats");

            migrationBuilder.DropColumn(
                name: "FolderId",
                table: "Chats");

            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "Chats");
        }
    }
}
