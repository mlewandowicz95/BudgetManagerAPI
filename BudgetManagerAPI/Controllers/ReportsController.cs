

using BudgetManagerAPI.Constants;
using BudgetManagerAPI.Data;
using BudgetManagerAPI.DTO;
using BudgetManagerAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BudgetManagerAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/reports")]
    public class ReportsController : BaseController
    {
        private readonly IPdfReportService _pdfReportService;
        private readonly AppDbContext _context;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(IPdfReportService pdfReportService, AppDbContext context, ILogger<ReportsController> logger)
        {
            _pdfReportService = pdfReportService;
            _context = context;
            _logger = logger;
        }

        [HttpGet("transactions")]
        [Authorize]
        public async Task<IActionResult> GetTransactionReport()
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
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User with ID {UserId} not found. TraceId: {TraceId}", userId, HttpContext.TraceIdentifier);
                    return NotFound(new ErrorResponseDto
                    {
                        Success = false,
                        Message = "User not found.",
                        ErrorCode = ErrorCodes.NotFound,
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                var transactions = await _context.Transactions
                    .Where(t => t.UserId == userId)
                    .Include(t => t.Category)
                    .ToListAsync();

                if (!transactions.Any())
                {
                    _logger.LogInformation("No transactions found for user {UserId}. TraceId: {TraceId}", userId, HttpContext.TraceIdentifier);
                    return NotFound(new ErrorResponseDto
                    {
                        Success = false,
                        Message = "No transactions found for the user.",
                        ErrorCode = ErrorCodes.NotFound,
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                // Generowanie raportu PDF
                var pdf = _pdfReportService.GenerateTransactionReport(transactions, user);

                if (pdf == null || pdf.Length == 0)
                {
                    _logger.LogError("Failed to generate PDF report for user {UserId}. TraceId: {TraceId}", userId, HttpContext.TraceIdentifier);
                    return StatusCode(500, new ErrorResponseDto
                    {
                        Success = false,
                        Message = "Failed to generate PDF report.",
                        ErrorCode = ErrorCodes.InternalServerError,
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                _logger.LogInformation("Transaction report generated successfully for user {UserId}. TraceId: {TraceId}", userId, HttpContext.TraceIdentifier);

                return File(pdf, "application/pdf", "TransactionReport.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while generating the transaction report for user {UserId}. TraceId: {TraceId}", userId, HttpContext.TraceIdentifier);
                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Message = "An error occurred while processing your request.",
                    ErrorCode = ErrorCodes.InternalServerError,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }


        [HttpGet("monthly-budgets")]
        [Authorize]
        public async Task<IActionResult> GetMonthlyBudgetReport()
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
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User with ID {UserId} not found. TraceId: {TraceId}", userId, HttpContext.TraceIdentifier);
                    return NotFound(new ErrorResponseDto
                    {
                        Success = false,
                        Message = "User not found.",
                        ErrorCode = ErrorCodes.NotFound,
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                var budgets = await _context.MonthlyBudgets
                    .Where(b => b.UserId == userId)
                    .Include(b => b.Category)
                    .ToListAsync();

                if (!budgets.Any())
                {
                    _logger.LogInformation("No budgets found for user {UserId}. TraceId: {TraceId}", userId, HttpContext.TraceIdentifier);
                    return NotFound(new ErrorResponseDto
                    {
                        Success = false,
                        Message = "No budgets found for the user.",
                        ErrorCode = ErrorCodes.NotFound,
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                // Generowanie raportu PDF
                var pdf = _pdfReportService.GenerateMonthlyBudgetReport(budgets, user);

                if (pdf == null || pdf.Length == 0)
                {
                    _logger.LogError("Failed to generate PDF report for user {UserId}. TraceId: {TraceId}", userId, HttpContext.TraceIdentifier);
                    return StatusCode(500, new ErrorResponseDto
                    {
                        Success = false,
                        Message = "Failed to generate PDF report.",
                        ErrorCode = ErrorCodes.InternalServerError,
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                _logger.LogInformation("Monthly budget report generated successfully for user {UserId}. TraceId: {TraceId}", userId, HttpContext.TraceIdentifier);

                return File(pdf, "application/pdf", "MonthlyBudgetReport.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while generating the monthly budget report for user {UserId}. TraceId: {TraceId}", userId, HttpContext.TraceIdentifier);

                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Message = "An error occurred while processing your request.",
                    ErrorCode = ErrorCodes.InternalServerError,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }



        [HttpGet("available")]
        [Authorize]
        public IActionResult GetAvailableReports()
        {
            try
            {
                var reports = new List<ReportInfoDto>
        {
            new ReportInfoDto
            {
                Name = "Transaction Report",
                Description = "This report shows the list of user transactions.",
                Endpoint = "/api/reports/transactions"
            },
            new ReportInfoDto
            {
                Name = "Monthly Budget Report",
                Description = "This report provides a summary of the monthly budget.",
                Endpoint = "/api/reports/monthly-budgets"
            }
        };

                _logger.LogInformation("Available reports fetched successfully. TraceId: {TraceId}", HttpContext.TraceIdentifier);

                return Ok(new SuccessResponseDto<List<ReportInfoDto>>
                {
                    Success = true,
                    Message = "Available reports fetched successfully.",
                    Data = reports,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching available reports. TraceId: {TraceId}", HttpContext.TraceIdentifier);

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
