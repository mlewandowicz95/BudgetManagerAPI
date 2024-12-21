namespace BudgetManagerAPI.DTO
{
    public class CreateMonthlyBudgetDto
    {
        public int CategoryId { get; set; }
        public decimal Amount { get; set; }
    }

    public class MonthlyBudgetStatusDto
    {
        public string CategoryName { get; set; }
        public decimal BudgetAmount { get; set; }
        public decimal SpentAmount { get; set; }
        public decimal RemainingAmount => BudgetAmount - SpentAmount;
        public bool IsOverBudget => SpentAmount > BudgetAmount;
    }
}
