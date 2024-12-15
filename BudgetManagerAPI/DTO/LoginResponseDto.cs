using System.ComponentModel.DataAnnotations;

namespace BudgetManagerAPI.DTO
{
    public class LoginResponseDto
    {
        public string Token { get; set; }
        public UserResponseDto User { get; set; }
    }

    public class LoginRequestDto
    {
        [EmailAddress]
        [Required]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }

    }
}
