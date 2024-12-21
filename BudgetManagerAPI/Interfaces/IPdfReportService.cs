using BudgetManagerAPI.Models;


namespace BudgetManagerAPI.Interfaces
{
    public interface IPdfReportService
    {
        byte[] GenerateTransactionReport(List<Transaction> transactions, User user);
        byte[] GenerateMonthlyBudgetReport(List<MonthlyBudget> budgets, User user);
    }
}
