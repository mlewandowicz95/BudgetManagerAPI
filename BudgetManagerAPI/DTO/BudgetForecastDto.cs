namespace BudgetManagerAPI.DTO
{
    public class BudgetForecastDto
    {
        public decimal PredicatedIncome { get; set; }
        public decimal PredicatedExpenses { get; set; }
        public decimal PredicatedBalance => PredicatedIncome - PredicatedExpenses;
    }
}
