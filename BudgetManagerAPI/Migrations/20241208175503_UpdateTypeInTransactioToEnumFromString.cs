using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetManagerAPI.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTypeInTransactioToEnumFromString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Konwertuj istniejące dane w kolumnie Type
            migrationBuilder.Sql("UPDATE Transactions SET Type = CASE WHEN Type = 'Expense' THEN 1 WHEN Type = 'Income' THEN 2 END");

            migrationBuilder.AlterColumn<int>(
                name: "Type",
                table: "Transactions",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Transactions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            // Przywróć dane do pierwotnych wartości tekstowych
            migrationBuilder.Sql("UPDATE Transactions SET Type = CASE WHEN Type = 1 THEN 'Expense' WHEN Type = 2 THEN 'Income' END");
        }
    }
}
