﻿using BudgetManagerAPI.Constants;
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
        [MaxLength(256)]
        public string Email { get; set; }
        [Required]
        [MaxLength(64)]
        public string PasswordHash { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = false;
        [MaxLength(64)]
        public string ActivationToken { get; set; } = Guid.NewGuid().ToString("N");
        public DateTime? LastLogin { get; set; }
        [MaxLength(64)]
        public string ResetToken { get; set; } = string.Empty;
        public DateTime? ResetTokenExpiry { get; set; }
        [Required]
        [MaxLength(20)]
        public string Role { get; set; } = Roles.User;

        public string NewEmail { get; set; } = string.Empty;
        public string EmailChangeToken { get; set; } = string.Empty;
        public DateTime? EmailChangeTokenExpiry { get; set; }

        public ICollection<Transaction> Transactions { get; set; }
        public ICollection<Category> Categories { get; set; }
        public ICollection<Goal> Goals { get; set; }
        public ICollection<MonthlyBudget> MonthlyBudgets { get; set; }
    }
}
