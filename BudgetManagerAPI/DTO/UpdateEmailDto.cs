using System.ComponentModel.DataAnnotations;

namespace BudgetManagerAPI.DTO
{
    public class UpdateEmailDto
    {
        [EmailAddress]
        [Required]
        public string NewEmail { get; set; }
    }
}
