using System.Collections.Generic;
using System.ComponentModel;

namespace BudgetManagerAPI.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public IEnumerable<Transaction> Transactions { get; set; }
        public IEnumerable<Category> Categories { get; set; }
        public IEnumerable<Goal> Goals { get; set; }
    }
}
