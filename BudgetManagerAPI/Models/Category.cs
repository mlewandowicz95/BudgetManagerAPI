namespace BudgetManagerAPI.Models
{
    public class Category
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public string Name { get; set; }

        public User? User { get; set; }
        public IEnumerable<Transaction> Transactions { get; set; }
        public ICollection<MonthlyBudget> MonthlyBudgets { get; set; }

    }
}
