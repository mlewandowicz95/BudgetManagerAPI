using BudgetManagerAPI.Constants;
using System.ComponentModel.DataAnnotations;

namespace BudgetManagerAPI.Validations
{
    public class RoleValidationAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var role = value as string;
            if (!Roles.All.Contains(role))
            {
                return new ValidationResult($"Invalid role. Allower roles are: {string.Join(", ", Roles.All)}");
            }

            return ValidationResult.Success;
        }
    }
}
