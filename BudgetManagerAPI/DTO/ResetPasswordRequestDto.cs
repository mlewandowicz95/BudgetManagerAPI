using System.ComponentModel.DataAnnotations;

namespace BudgetManagerAPI.DTO
{
    public class ResetPasswordRequestDto
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress]
        public string Email { get; set; }
    }
}
