using System.ComponentModel.DataAnnotations;

namespace BudgetManagerAPI.Models
{
    public class MonthlyBudget
    {
        public int Id { get; set; }
        [Required]
        public int UserId { get; set; }
        public int CategoryId { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal Amount { get; set; }

        [Required]
        public DateTime Month { get; set; }

        public User User { get; set; }
        public Category Category { get; set; }
    }
}
