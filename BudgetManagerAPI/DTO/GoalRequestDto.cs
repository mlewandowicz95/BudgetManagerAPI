using System.ComponentModel.DataAnnotations;

namespace BudgetManagerAPI.DTO
{

    public class GoalRequestDto
    {
        public int UserId { get; set; }
        [Required]
        public string Name { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Please enter a value bigger than {0.01}")]
        public decimal TargetAmount { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Please enter a value bigger than {0.01}")]
        public decimal CurrentProgress { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DueDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
