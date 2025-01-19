using BudgetManagerAPI.Validations;
using System.ComponentModel.DataAnnotations;

namespace BudgetManagerAPI.DTO.Admin
{
    public class UpdateRoleRequestDto
    {
        [Required]
        [RoleValidation]
        public string Role { get; set; } = "Pro";
    }
}
