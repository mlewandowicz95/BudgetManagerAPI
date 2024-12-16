using System.ComponentModel.DataAnnotations;

namespace BudgetManagerAPI.DTO
{
    public class ResetPasswordRequestDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}
