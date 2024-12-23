using BudgetManagerAPI.Data;
using BudgetManagerAPI.DTO;
using BudgetManagerAPI.Enums;
using BudgetManagerAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BudgetManagerAPI.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly ILogger<DashboardController> _logger;


        public DashboardController(AppDbContext context, ILogger<DashboardController> logger)
        {
            _context = context;
            _logger = logger;
        }


        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboardSummary()
        {
            int parsedUsedId = GetParseUserId();
            if(parsedUsedId == 0)
            {
                return Unauthorized(new { Message = "User is not authenticated." });
            }

            // Pobierz transakcje z bieżacego miesiaca
            var currentDate = DateTime.UtcNow;
            var transactions = await _context.Transactions
                .Where(t => t.UserId == parsedUsedId && t.Date.Month == currentDate.Month && t.Date.Year == currentDate.Year)
                .Include(t => t.Category)
                .ToListAsync();

            // podsumowanie p[rzytchodów i wydatków
            var totalIncome = transactions.Where(t => t.Type == Enums.TransactionType.Income).Sum(t => t.Amount);
            var totalExpenses = transactions.Where(t => t.Type == Enums.TransactionType.Expense).Sum(t => t.Amount);


            // Ostatbnie transakcje (maks. 5)
            var recentTransactions = transactions
                .OrderByDescending(t => t.Date)
                .Take(5)
                .Select(t => new TransactionDto
                {
                    Date = t.Date,
                    Amount = t.Amount,
                    CategoryName = t.Category.Name,
                    Description = t.Description,
                    Type = t.Type.ToString()
                })
                .ToList();

            // download all goals
            var goals = await _context.Goals
                .Where(g => g.UserId == parsedUsedId)
                .OrderBy(g => g.DueDate ?? DateTime.MaxValue) // Najpierw cele z terminem
                .ThenBy(g => g.CurrentProgress / g.TargetAmount) // następnie wg procentu realizacji
                .ToListAsync();

            var goalsDtos = goals.Select(g => new GoalDto
            {
                Name = g.Name,
                TargetAmount = g.TargetAmount,
                CurrentProgress = g.CurrentProgress,
                DueDate = g.DueDate,
            }).ToList();


            // build dashboard
            var dashboardSummary = new DashbordSummaryDto
            {
                TotalExpenses = totalExpenses,
                TotalIncome = totalIncome,
                RecentTransactions = recentTransactions,
                SavingGoals = goalsDtos
            };

            return Ok(dashboardSummary);
        }



        [HttpGet("dashboard/expenses-by-category")]
        public async Task<IActionResult> GetExpensesByCategory()
        {
            int parsedUserId = GetParseUserId();
            if (parsedUserId == 0)
            {
                return Unauthorized(new { Message = "User is not authenticated." });
            }


            var categoryExpenses = await _context.Transactions
                .Where(t => t.UserId == parsedUserId && t.Type == TransactionType.Expense)
                .GroupBy(t => t.Category.Name)
                .Select(g => new
                {
                    Category = g.Key,
                    TotalAmount = g.Sum(t => t.Amount)
                })
                .ToListAsync();

            return Ok(categoryExpenses);
        }



        [HttpGet("dashboard/balance-per-month")]
        public async Task<IActionResult> GetBalancePerMonth()
        {
            int parsedUserId = GetParseUserId();
            if (parsedUserId == 0)
            {
                return Unauthorized(new { Message = "User is not authenticated." });
            }

            var monthlyData = await _context.Transactions
                .Where(t => t.UserId == parsedUserId)
                .GroupBy(t => new { t.Date.Year, t.Date.Month })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Income = g.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount),
                    Expenses = g.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount)
                })
                .OrderByDescending(m => m.Month)
                .Take(12)
                .ToListAsync();

            if(monthlyData.Count == 0)
            {
                return Ok(new { Message = "Data is empty." });
            }

            var formattedData = monthlyData.Select(x => new
            {
                Month = new DateTime(x.Year, x.Month, 1).ToString("MMMM yyyy"),
                x.Income,
                x.Expenses
            }).ToList();

            return Ok(formattedData);
        }


        [HttpGet("dashboard/budget-forecast")]
        public async Task<IActionResult> GetBudgetForecast()
        {
            int parsedUserId = GetParseUserId();
            if(parsedUserId == 0)
            {
                return Unauthorized(new { Message = "User is not authenticated." });
            }

            var recurringTransactions = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == parsedUserId && t.IsRecurring)
                .ToListAsync();

            decimal predicatedIncome = 0m;
            decimal predicatedExpenses = 0m;

            foreach(var transaction in recurringTransactions)
            {
                switch(transaction.Type)
                {
                    case TransactionType.Income:
                        predicatedIncome += CalculateRecurringAmount(transaction, DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
                        break;

                    case TransactionType.Expense:
                        predicatedExpenses += CalculateRecurringAmount(transaction, DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
                        break;
                }
            }

            var forecast = new BudgetForecastDto
            {
                PredicatedExpenses = predicatedExpenses,
                PredicatedIncome = predicatedIncome
            };

            return Ok(forecast);
        }

        [HttpGet("financial-indicators")]
        public async Task<IActionResult> GetFinancialIndicators()
        {
            try
            {
                var userId = GetParseUserId();
                if(userId == 0)
                {
                    return Unauthorized(new { Message = "Error in UserId" });
                }

                var transactions = await _context.Transactions
                    .Where(t => t.UserId == userId)
                    .ToListAsync();

                if(transactions.Count == 0)
                {
                    return Ok(new FinancialIndicatorsDto
                    {
                        SavingsPercentage = 0,
                        ExpensesToIncomeRatio = 0,
                        AverageMonthlyExpenses = 0,
                        AverageMonthlyIncome = 0
                    });
                }

                var income = transactions.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
                var expenses = transactions.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);

                var firstTransactionDate = transactions.Min(t => t.Date);
                var monthsActive = Math.Max(1, (int)((DateTime.UtcNow - firstTransactionDate).TotalDays / 30));

                var averageMonthlyIncome = income / monthsActive;
                var averageMonthlyExpenses = expenses / monthsActive;

                var savingsPercentage = income > 0 ? ((income - expenses) / income) * 100 : 0;
                var expensesToIncomeRatio = income > 0 ? (expenses - income) * 100 : 0;

                var indicators = new FinancialIndicatorsDto
                {
                    SavingsPercentage = savingsPercentage,
                    ExpensesToIncomeRatio = expensesToIncomeRatio,
                    AverageMonthlyExpenses = averageMonthlyExpenses,
                    AverageMonthlyIncome = averageMonthlyIncome
                };

                return Ok(indicators);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching financial indicators.");
                return StatusCode(500, new { Message = "An error occurred while processing your request." });
            }
        }

        private decimal CalculateRecurringAmount(Transaction transaction, DateTime startDate, DateTime endDate)
        {
            if (transaction.Category == null || string.IsNullOrEmpty(transaction.Category.Name))
            {
                throw new InvalidOperationException("Transaction has no valid category.");
            }


            decimal total = 0;
            switch(transaction.Category.Name.ToLower())
            {
                case "daily":
                    total = transaction.Amount * (endDate - startDate).Days;
                    break;

                case "weekly":
                    total = transaction.Amount * (decimal)Math.Ceiling((endDate - startDate).TotalDays / 7);
                    break;

                case "monthly":
                    total = transaction.Amount;
                    break;

                default:
                    throw new InvalidOperationException("Unsupported recurring interval.");

            }
            return total;
        }
    }
}
