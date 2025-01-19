using BudgetManagerAPI.Constants;
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
            var userId = GetParseUserId();
            if (userId == 0)
            {
                return Unauthorized(new ErrorResponseDto
                {
                    Success = false,
                    Message = "User is not authenticated.",
                    ErrorCode = ErrorCodes.Unathorized,
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            if (!ModelState.IsValid)
            {
                _logger.LogError("Invalid model state. TraceId: {TraceId}", HttpContext.TraceIdentifier);
                return BadRequest(new ErrorResponseDto
                {
                    Success = false,
                    Message = "Invalid request data.",
                    ErrorCode = ErrorCodes.ValidationError,
                    TraceId = HttpContext.TraceIdentifier,
                    Errors = ModelState.ToDictionary(
                        key => key.Key,
                        value => value.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                    )
                });
            }

            try
            {
                var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

                // Sprawdzanie czy budżet już istnieje
                var existingBudget = await _context.MonthlyBudgets
                    .FirstOrDefaultAsync(b => b.UserId == userId && b.CategoryId == dto.CategoryId && b.Month == month);

                if (existingBudget != null)
                {
                    _logger.LogWarning("Budget for category {CategoryId} already exists for user {UserId}. TraceId: {TraceId}", dto.CategoryId, userId, HttpContext.TraceIdentifier);
                    return Conflict(new ErrorResponseDto
                    {
                        Success = false,
                        Message = "Budget for this category already exists.",
                        ErrorCode = ErrorCodes.Conflict,
                        TraceId = HttpContext.TraceIdentifier
                    });
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

                _logger.LogInformation("Monthly budget added successfully for user {UserId}. TraceId: {TraceId}", userId, HttpContext.TraceIdentifier);

                return Ok(new SuccessResponseDto<CreateMonthlyBudgetResponseDto>
                {
                    Success = true,
                    Message = "Monthly budget added successfully.",
                    TraceId = HttpContext.TraceIdentifier,
                    Data = new CreateMonthlyBudgetResponseDto
                    {
                        Id = budget.Id,
                        Amount = budget.Amount,
                        UserId = budget.UserId,
                        Month = budget.Month,
                        CategoryId = budget.CategoryId,
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating the monthly budget for user {UserId}. TraceId: {TraceId}", userId, HttpContext.TraceIdentifier);

                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Message = "An error occurred while processing your request.",
                    ErrorCode = ErrorCodes.InternalServerError,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }


        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetBudgetStatus()
        {
            var userId = GetParseUserId();
            if (userId == 0)
            {
                return Unauthorized(new ErrorResponseDto
                {
                    Success = false,
                    Message = "User is not authenticated.",
                    ErrorCode = ErrorCodes.Unathorized,
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            try
            {
                var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

                // Pobierz budżety użytkownika dla bieżącego miesiąca
                var budgets = await _context.MonthlyBudgets
                    .Where(b => b.UserId == userId && b.Month == month)
                    .Include(b => b.Category)
                    .ToListAsync();

                // Pobierz transakcje użytkownika dla bieżącego miesiąca
                var transactions = await _context.Transactions
                    .Where(t => t.UserId == userId && t.Date.Year == month.Year && t.Date.Month == month.Month)
                    .ToListAsync();

                // Oblicz statusy budżetów
                var status = budgets.Select(budget =>
                {
                    var spent = transactions
                        .Where(t => t.CategoryId == budget.CategoryId)
                        .Sum(t => t.Amount);

                    return new MonthlyBudgetStatusDto
                    {
                        CategoryName = budget.Category?.Name ?? "Unknown Category",
                        BudgetAmount = budget.Amount,
                        SpentAmount = spent
                    };
                }).ToList();

                _logger.LogInformation("Successfully fetched budget status for user {UserId}. TraceId: {TraceId}", userId, HttpContext.TraceIdentifier);

                return Ok(new SuccessResponseDto<List<MonthlyBudgetStatusDto>>
                {
                    Success = true,
                    Message = "Budget status fetched successfully.",
                    Data = status,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching the budget status for user {UserId}. TraceId: {TraceId}", userId, HttpContext.TraceIdentifier);

                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Message = "An error occurred while processing your request.",
                    ErrorCode = ErrorCodes.InternalServerError,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }

    }
}
