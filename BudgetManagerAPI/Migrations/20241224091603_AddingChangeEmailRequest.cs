using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetManagerAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddingChangeEmailRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmailChangeToken",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailChangeTokenExpiry",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NewEmail",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailChangeToken",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmailChangeTokenExpiry",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NewEmail",
                table: "Users");
        }
    }
}
