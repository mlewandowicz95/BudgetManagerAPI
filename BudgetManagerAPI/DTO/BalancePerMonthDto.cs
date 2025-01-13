namespace BudgetManagerAPI.DTO
{
    public class BalancePerMonthDto
    {
        public string YearMonth { get; set; }
        public decimal Income { get; set; }
        public decimal Expenses { get; set; }
    }
}
