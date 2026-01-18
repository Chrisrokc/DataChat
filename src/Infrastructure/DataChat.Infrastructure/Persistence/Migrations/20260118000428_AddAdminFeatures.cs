using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataChat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AnnouncementDismissible",
                table: "SystemConfiguration",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AnnouncementEnabled",
                table: "SystemConfiguration",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "AnnouncementEndDate",
                table: "SystemConfiguration",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnnouncementMessage",
                table: "SystemConfiguration",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AnnouncementStartDate",
                table: "SystemConfiguration",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnnouncementType",
                table: "SystemConfiguration",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "info");

            migrationBuilder.AddColumn<bool>(
                name: "CostAlertEnabled",
                table: "SystemConfiguration",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "CostAlertThreshold",
                table: "SystemConfiguration",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 80m);

            migrationBuilder.AddColumn<decimal>(
                name: "CostPerInputToken",
                table: "SystemConfiguration",
                type: "decimal(18,10)",
                precision: 18,
                scale: 10,
                nullable: false,
                defaultValue: 0.00001m);

            migrationBuilder.AddColumn<decimal>(
                name: "CostPerOutputToken",
                table: "SystemConfiguration",
                type: "decimal(18,10)",
                precision: 18,
                scale: 10,
                nullable: false,
                defaultValue: 0.00003m);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyCostBudget",
                table: "SystemConfiguration",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "InputTokens",
                table: "ChatMessages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OutputTokens",
                table: "ChatMessages",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "SystemConfiguration",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "AnnouncementDismissible", "AnnouncementEnabled", "AnnouncementEndDate", "AnnouncementMessage", "AnnouncementStartDate", "AnnouncementType", "CostAlertEnabled", "CostAlertThreshold", "CostPerInputToken", "CostPerOutputToken" },
                values: new object[] { true, false, null, null, null, "info", false, 80m, 0.00001m, 0.00003m });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnnouncementDismissible",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "AnnouncementEnabled",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "AnnouncementEndDate",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "AnnouncementMessage",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "AnnouncementStartDate",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "AnnouncementType",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "CostAlertEnabled",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "CostAlertThreshold",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "CostPerInputToken",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "CostPerOutputToken",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "MonthlyCostBudget",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "InputTokens",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "OutputTokens",
                table: "ChatMessages");
        }
    }
}
