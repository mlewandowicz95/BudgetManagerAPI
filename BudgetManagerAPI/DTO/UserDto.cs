using BudgetManagerAPI.Constants;
using System.ComponentModel.DataAnnotations;

namespace BudgetManagerAPI.DTO
{
    public class UserRequestDto
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$",
    ErrorMessage = "Password must have minimum eight characters, at least one uppercase letter, one lowercase letter, one number, and one special character.")]
        public string Password { get; set; }

        [Required(ErrorMessage = "ConfirmPassword is required")]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; }

        public string Role { get; set; } = Roles.User;

    }

    public class UserResponseDto
    {
        public int Id { get; set; }

        public string Email { get; set; }
        public string Role { get; set; }
    }
}
