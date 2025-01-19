namespace BudgetManagerAPI.Models
{
    public class Goal
    {
        public int Id { get; set; }
        public int UserId {  get; set; }
        public string Name { get; set; }
        public decimal TargetAmount { get; set; }
        public decimal CurrentProgress { get; set; } = 0m;
        public DateTime? DueDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public User User { get; set; }
        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}
