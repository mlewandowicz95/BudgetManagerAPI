namespace BudgetManagerAPI.DTO
{
    public class FinancialIndicatorsDto
    {
        public decimal SavingsPercentage { get; set; } // Oszczędności jako procent przychodów
        public decimal ExpensesToIncomeRatio { get; set; } // Stosunek wydatków do przychodów
        public decimal AverageMonthlyExpenses { get; set; } // Średnie miesięczne wydatki
        public decimal AverageMonthlyIncome { get; set; } // Średnie miesięczne przychody
    }
}
