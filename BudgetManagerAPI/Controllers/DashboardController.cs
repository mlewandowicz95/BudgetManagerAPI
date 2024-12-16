using BudgetManagerAPI.Data;
using BudgetManagerAPI.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BudgetManagerAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        [Authorize]
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboardSummary()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if(string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { Message = "User is not authenticated." });
            }

            int parsedUsedId = int.Parse(userId);

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
                .ToListAsync();

            var goalsDtos = goals.Select(g => new GoalDto
            {
                Name = g.Name,
                TargetAmount = g.TargetAmount,
                CurrentProgress = g.CurrentProgress,
                DueDate = g.DueDate
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

    }
}
