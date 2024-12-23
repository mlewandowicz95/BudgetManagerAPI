using BudgetManagerAPI.Data;
using BudgetManagerAPI.DTO;
using BudgetManagerAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BudgetManagerAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MonthlyBudgetController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly ILogger<MonthlyBudgetController> _logger;


        public MonthlyBudgetController(AppDbContext context, ILogger<MonthlyBudgetController> logger)
        {
            _context = context;
            _logger = logger;
        }


        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateBudget([FromBody] CreateMonthlyBudgetDto dto)
        {
            try
            {
                int userId = GetParseUserId();
                if(userId == 0)
                {
                    _logger.LogError("Error user id.");
                    return BadRequest(new { Message = "Error user id." });
                }

                var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

                var existingBudget = await _context.MonthlyBudgets
                    .FirstOrDefaultAsync(b => b.UserId == userId && b.CategoryId == dto.CategoryId && b.Month == month);

                if(existingBudget != null)
                {
                    _logger.LogWarning("Budget for this category already exists.");
                    return BadRequest(new { Message = "Budget for this category already exists." });
                }

                var budget = new MonthlyBudget
                {
                    UserId = userId,
                    CategoryId = dto.CategoryId,
                    Amount = dto.Amount,
                    Month = month
                };

                _context.MonthlyBudgets.Add(budget);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Monthly budget added successfully.");
                return Ok(new { Message = "Monthly budget added successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occured while creating the monthly budget.");
                return StatusCode(500, new { Message = "An error occured while processing your request." });
            }
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetBudgetStatus()
        {
            try
            {
                int userId = GetParseUserId();
                if (userId == 0)
                {
                    _logger.LogError("Error user id.");
                    return BadRequest(new { Message = "Error user id." });
                }

                var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

                var budgets = await _context.MonthlyBudgets
                    .Where(b => b.UserId == userId && b.Month == month)
                    .Include(b => b.Category)
                    .ToListAsync();

                var transactions = await _context.Transactions
                    .Where(t => t.UserId == userId && t.Date.Year == month.Year && t.Date.Month == month.Month)
                    .ToListAsync();

                var status = budgets.Select(budget =>
                {
                    var spent = transactions
                    .Where(t => t.CategoryId == budget.CategoryId)
                    .Sum(t => t.Amount);


                    return new MonthlyBudgetStatusDto
                    {
                        CategoryName = budget.Category.Name,
                        BudgetAmount = budget.Amount,
                        SpentAmount = spent
                    };
                }).ToList();

                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching the budget status.");
                return StatusCode(500, new { Message = "An error occurred while processing your request." });
            }
        }
    }
}
