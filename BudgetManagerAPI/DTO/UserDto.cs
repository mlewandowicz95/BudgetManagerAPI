using System.ComponentModel.DataAnnotations;

namespace BudgetManagerAPI.DTO
{
    public class UserRequestDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
        public string PasswordHash { get; set; }

    }

    public class UserResponseDto
    {
        public int Id { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }
        public string PasswordHash { get; set; }
    }
}
