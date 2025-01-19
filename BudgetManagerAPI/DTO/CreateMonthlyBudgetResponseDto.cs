namespace BudgetManagerAPI.DTO
{
    public class CreateMonthlyBudgetResponseDto
    {
        public int Id { get; set; } 
        public int UserId { get; set; } 
        public int CategoryId { get; set; } 
        public decimal Amount { get; set; }
        public DateTime Month { get; set; } 
    }
}
