using BudgetManagerAPI.Validations;
using System.ComponentModel.DataAnnotations;

namespace BudgetManagerAPI.DTO.Admin
{
    public class UpdateRoleDto
    {
        [Required]
        [RoleValidation]
        public string Role { get; set; } = "Pro";
    }
}
