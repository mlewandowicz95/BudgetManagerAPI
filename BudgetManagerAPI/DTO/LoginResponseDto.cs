using System.ComponentModel.DataAnnotations;

namespace BudgetManagerAPI.DTO
{
    public class LoginResponseDto
    {
        public string Token { get; set; }
    }

    public class LoginRequestDto
    {
        [EmailAddress]
        [Required(ErrorMessage = "Email is required.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        public string Password { get; set; }

    }
}
