

namespace BudgetManagerAPI.DTO.Admin
{
    public class UpdateUserDto
    {
        public string Email { get; set; }
        public string Role { get; set; }

        public bool isActive { get; set; }
    }
}
