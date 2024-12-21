using System.ComponentModel.DataAnnotations;

namespace BudgetManagerAPI.Models
{
    public class Alert
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Message { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; } = false;

        public User User { get; set; }
    }
}
