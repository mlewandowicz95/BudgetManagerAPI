using BudgetManagerAPI.Constants;
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


        [HttpGet]
        public async Task<IActionResult> GetDashboardSummary()
        {
            int parsedUsedId = GetParseUserId();
            if (parsedUsedId == 0)
            {
                return Unauthorized(new ErrorResponseDto
                {
                    Success = false,
                    TraceId = HttpContext.TraceIdentifier,
                    Message = "User is not authenticated.",
                    ErrorCode = ErrorCodes.Unathorized
                });
            }


            var currentDate = DateTime.UtcNow;
            try
            {


                var transactions = await _context.Transactions
                    .Where(t => t.UserId == parsedUsedId)
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
                        Id = t.Id,
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

                return Ok(new SuccessResponseDto<DashbordSummaryDto>
                {
                    Data = dashboardSummary,
                    Success = true,
                    Message = "Poprawnie pobrano dane.",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while dowloading dashboard summary. TraceId: {TraceId}", HttpContext.TraceIdentifier);
                return StatusCode(500, new ErrorResponseDto
                {

                    Success = false,
                    Message = "An error occurred while processing your request. Please try again later.",
                    ErrorCode = ErrorCodes.InternalServerError,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }



        [HttpGet("expenses-by-category")]
        public async Task<IActionResult> GetExpensesByCategory()
        {
            int parsedUserId = GetParseUserId();
            if (parsedUserId == 0)
            {
                return Unauthorized(new ErrorResponseDto
                {
                    Success = false,
                    TraceId = HttpContext.TraceIdentifier,
                    Message = "User is not authenticated.",
                    ErrorCode = ErrorCodes.Unathorized
                });
            }


            try
            {
                var categoryExpenses = await _context.Transactions
                    .Where(t => t.UserId == parsedUserId && t.Type == TransactionType.Expense)
                    .GroupBy(t => t.Category.Name)
                    .Select(g => new CategoryExpenseDto
                    {
                        Category = g.Key,
                        TotalAmount = g.Sum(t => t.Amount)
                    })
                    .ToListAsync();

                return Ok(new SuccessResponseDto<List<CategoryExpenseDto>>
                {
                    Success = true,
                    Message = "Data retrieved successfully.",
                    TraceId = HttpContext.TraceIdentifier,
                    Data = categoryExpenses
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while dowloading expenses by category. TraceId: {TraceId}", HttpContext.TraceIdentifier);
                return StatusCode(500, new ErrorResponseDto
                {

                    Success = false,
                    Message = "An error occurred while processing your request. Please try again later.",
                    ErrorCode = ErrorCodes.InternalServerError,
                    TraceId = HttpContext.TraceIdentifier
                });
            }

        }



        [HttpGet("balance-per-month")]
        public async Task<IActionResult> GetBalancePerMonth()
        {
            int parsedUserId = GetParseUserId();
            if (parsedUserId == 0)
            {
                return Unauthorized(new ErrorResponseDto
                {
                    Success = false,
                    TraceId = HttpContext.TraceIdentifier,
                    Message = "User is not authenticated.",
                    ErrorCode = ErrorCodes.Unathorized
                });
            }

            try
            {
                // Pobranie danych z bazy i przetwarzanie na poziomie klienta
                var transactions = await _context.Transactions
                    .Where(t => t.UserId == parsedUserId)
                    .ToListAsync();

                var monthlyData = transactions
                    .GroupBy(t => new { t.Date.Year, t.Date.Month })
                    .OrderBy(g => g.Key.Year)
                    .ThenBy(g => g.Key.Month)
                    .Select(g => new
                    {
                        Year = g.Key.Year,
                        Month = g.Key.Month,
                        Income = g.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount),
                        Expenses = g.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount)
                    })
                    .Take(12)
                    .ToList();

                if (!monthlyData.Any())
                {
                    return Ok(new SuccessResponseDto<List<BalancePerMonthDto>>
                    {
                        Success = true,
                        Message = "No data found.",
                        TraceId = HttpContext.TraceIdentifier,
                        Data = new List<BalancePerMonthDto>()
                    });
                }

                var formattedData = monthlyData.Select(x => new BalancePerMonthDto
                {
                    YearMonth = new DateTime(x.Year, x.Month, 1).ToString("MMMM yyyy"),
                    Income = x.Income,
                    Expenses = x.Expenses
                }).ToList();

                return Ok(new SuccessResponseDto<List<BalancePerMonthDto>>
                {
                    Success = true,
                    Message = "Balance data retrieved successfully.",
                    TraceId = HttpContext.TraceIdentifier,
                    Data = formattedData
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while downloading balance per month. TraceId: {TraceId}", HttpContext.TraceIdentifier);
                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Message = "An error occurred while processing your request. Please try again later.",
                    ErrorCode = ErrorCodes.InternalServerError,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }



        [HttpGet("budget-forecast")]
        public async Task<IActionResult> GetBudgetForecast()
        {
            int parsedUserId = GetParseUserId();
            if (parsedUserId == 0)
            {
                return Unauthorized(new ErrorResponseDto
                {
                    Success = false,
                    TraceId = HttpContext.TraceIdentifier,
                    Message = "User is not authenticated.",
                    ErrorCode = ErrorCodes.Unathorized
                });
            }

            try
            {
                var recurringTransactions = await _context.Transactions
                    .Include(t => t.Category)
                    .Where(t => t.UserId == parsedUserId && t.IsRecurring)
                    .ToListAsync();

                decimal predictedIncome = recurringTransactions
                    .Where(t => t.Type == TransactionType.Income)
                    .Sum(t => CalculateRecurringAmount(t, DateTime.UtcNow, DateTime.UtcNow.AddMonths(1)));

                decimal predictedExpenses = recurringTransactions
                    .Where(t => t.Type == TransactionType.Expense)
                    .Sum(t => CalculateRecurringAmount(t, DateTime.UtcNow, DateTime.UtcNow.AddMonths(1)));

                var forecast = new BudgetForecastDto
                {
                    PredicatedExpenses = predictedExpenses,
                    PredicatedIncome = predictedIncome
                };

                return Ok(new SuccessResponseDto<BudgetForecastDto>
                {
                    Success = true,
                    Message = "Budget forecast retrieved successfully.",
                    TraceId = HttpContext.TraceIdentifier,
                    Data = forecast
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while calculating the budget forecast. TraceId: {TraceId}", HttpContext.TraceIdentifier);
                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Message = "An error occurred while processing your request. Please try again later.",
                    ErrorCode = ErrorCodes.InternalServerError,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }


        [HttpGet("financial-indicators")]
        public async Task<IActionResult> GetFinancialIndicators()
        {
            try
            {
                // Pobranie UserId
                var userId = GetParseUserId();
                if (userId == 0)
                {
                    return Unauthorized(new ErrorResponseDto
                    {
                        Success = false,
                        TraceId = HttpContext.TraceIdentifier,
                        Message = "User is not authenticated.",
                        ErrorCode = ErrorCodes.Unathorized
                    });
                }

                // Pobranie transakcji użytkownika
                var transactions = await _context.Transactions
                    .Where(t => t.UserId == userId)
                    .ToListAsync();

                // Obsługa przypadku braku danych
                if (!transactions.Any())
                {
                    return Ok(new SuccessResponseDto<FinancialIndicatorsDto>
                    {
                        Success = true,
                        Message = "No transactions found.",
                        TraceId = HttpContext.TraceIdentifier,
                        Data = new FinancialIndicatorsDto
                        {
                            SavingsPercentage = 0,
                            ExpensesToIncomeRatio = 0,
                            AverageMonthlyExpenses = 0,
                            AverageMonthlyIncome = 0
                        }
                    });
                }

                // Obliczenia wskaźników
                var income = transactions.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
                var expenses = transactions.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);

                var firstTransactionDate = transactions.Min(t => t.Date);
                var monthsActive = Math.Max(1, (int)((DateTime.UtcNow - firstTransactionDate).TotalDays / 30));

                var averageMonthlyIncome = income / monthsActive;
                var averageMonthlyExpenses = expenses / monthsActive;
                var savingsPercentage = income > 0 ? ((income - expenses) / income) * 100 : 0;
                var expensesToIncomeRatio = income > 0 ? (expenses / income) * 100 : 0;

                // Utworzenie obiektu DTO
                var indicators = new FinancialIndicatorsDto
                {
                    SavingsPercentage = savingsPercentage,
                    ExpensesToIncomeRatio = expensesToIncomeRatio,
                    AverageMonthlyExpenses = averageMonthlyExpenses,
                    AverageMonthlyIncome = averageMonthlyIncome
                };

                // Zwrócenie odpowiedzi
                return Ok(new SuccessResponseDto<FinancialIndicatorsDto>
                {
                    Success = true,
                    Message = "Financial indicators calculated successfully.",
                    TraceId = HttpContext.TraceIdentifier,
                    Data = indicators
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching financial indicators. TraceId: {TraceId}", HttpContext.TraceIdentifier);
                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Message = "An error occurred while processing your request. Please try again later.",
                    ErrorCode = ErrorCodes.InternalServerError,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }


        private decimal CalculateRecurringAmount(Transaction transaction, DateTime startDate, DateTime endDate)
        {
            if (transaction.Category == null || string.IsNullOrEmpty(transaction.Category.Name))
            {
                throw new InvalidOperationException("Transaction has no valid category.");
            }


            decimal total = 0;
            switch (transaction.Category.Name.ToLower())
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
                    _logger.LogWarning($"Category {transaction.Category.Name} is not explicitly supported. Assuming monthly.");
                    total = transaction.Amount;
                    break;

            }
            return total;
        }
    }
}
