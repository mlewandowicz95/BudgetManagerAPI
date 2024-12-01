using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BudgetManagerAPI.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public IEnumerable<Transaction> Transactions { get; set; }
        public IEnumerable<Category> Categories { get; set; }
        public IEnumerable<Goal> Goals { get; set; }
    }
}
