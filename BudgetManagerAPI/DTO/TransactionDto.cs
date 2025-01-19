using BudgetManagerAPI.Enums;


namespace BudgetManagerAPI.DTO
{
    public class TransactionRequestDto
    {
        public int UserId { get; set; }
        public decimal Amount { get; set; }
        public TransactionType Type { get; set; } // "Income" or "Expense", 
        public int CategoryId { get; set; }
        public int? GoalId { get; set; }
        public string? Description { get; set; }
        public bool IsRecurring { get; set; } = false;
        public DateTime Date { get; set; }
    }

    public class TransactionResponseDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int? GoalId { get; set; }
        public decimal Amount { get; set; }
        public TransactionType Type { get; set; } // "Income" or "Expense", 
        public int CategoryId { get; set; }
        public string? Description { get; set; }
        public bool IsRecurring { get; set; } = false;
        public DateTime Date { get; set; }
    }
}
