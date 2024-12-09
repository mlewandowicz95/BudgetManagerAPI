﻿using System.ComponentModel.DataAnnotations;

namespace BudgetManagerAPI.DTO
{
    public class UserRequestDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$",
    ErrorMessage = "Password must have minimum eight characters, at least one uppercase letter, one lowercase letter, one number, and one special character.")]
        public string Password { get; set; }

        [Required]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; }

    }

    public class UserResponseDto
    {
        public int Id { get; set; }

        public string Email { get; set; }
    }
}
