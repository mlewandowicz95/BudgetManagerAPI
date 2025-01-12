using System.ComponentModel.DataAnnotations;

namespace BudgetManagerAPI.DTO
{
    public class ResendActivationLinkRequestDto
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; }
    }
}
