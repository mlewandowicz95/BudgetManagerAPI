using System.ComponentModel.DataAnnotations;

namespace BudgetManagerAPI.DTO
{
    public class ResendActivationRequestDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}
