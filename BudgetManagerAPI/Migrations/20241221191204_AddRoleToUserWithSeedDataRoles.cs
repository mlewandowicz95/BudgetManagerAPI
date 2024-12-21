using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetManagerAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleToUserWithSeedDataRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "User");

            // Default users:
            // Passwords:
            // Admin: 
            // Pro: Pro123!@
            // User: User123!@
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "Email", "PasswordHash", "Role", "CreatedAt", "IsActive", "ActivationToken", "ResetToken" },
                values: new object[,]
                {
                    { 1, "admin@example.com", "$2a$12$y2hgK4Pzm0OItuW.zEQ3NeGCcuHIXNTW25MGeLh3bCH/IZIrhVuQ6", "Admin", new DateTime(2024, 12, 21, 0, 0, 0, DateTimeKind.Utc), true, string.Empty, string.Empty },
                    { 2, "pro@example.com", "$2a$12$dbW3y8kIdLrOMsdKGzUY3.i1u.baIlIPygAq.451hFqn5Jh9mZCIy", "Pro", new DateTime(2024, 12, 21, 0, 0, 0, DateTimeKind.Utc), true, string.Empty, string.Empty },
                    { 3, "user@example.com", "$2a$12$LTyml/LK9rAqIWo/fcSuh.EAguVJBmBnVT3Qe7ME/V.w7IycrvBWO", "User", new DateTime(2024, 12, 21, 0, 0, 0, DateTimeKind.Utc), true, string.Empty, string.Empty }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Users");
        }
    }
}
