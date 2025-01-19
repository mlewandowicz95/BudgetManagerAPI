using BudgetManagerAPI.Enums;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BudgetManagerAPI.Models
{
    public class Transaction
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

        public User User { get; set; }
        public Category Category { get; set; }
        public Goal Goal { get; set; }
    }

}
