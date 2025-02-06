namespace BudgetManagerAPI.DTO
{
    public class DashbordSummaryDto
    {
        public decimal TotalIncome { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal Balance => TotalIncome - TotalExpenses;

        public List<TransactionDto> RecentTransactions { get; set; } // last transactions
        public List<GoalDto> SavingGoals { get; set; } // cele oszczednosciowe
    }

    public class TransactionDto
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string CategoryName { get; set; }
        public string? Description { get; set; }
        public string Type { get; set; } // "Income" or "Expense
    }

    public class GoalDto
    {
        public string Name { get; set; }
        public decimal TargetAmount { get; set; }
        public decimal CurrentProgress { get; set; }
        public decimal ProgressPercentage => (CurrentProgress / TargetAmount) * 100;
        public DateTime? DueDate { get; set; }
        public bool IsCloseToCompletion => ProgressPercentage >= 80;
    }
}
