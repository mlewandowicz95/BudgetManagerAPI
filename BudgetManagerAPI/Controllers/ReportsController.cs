

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
        public async Task<IActionResult> GetTransactionReport()
        {
            var userId = GetParseUserId();
            if (userId == 0)
            {
                return Unauthorized(new { Message = "Error UserId." });
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User not found.");
                return NotFound(new { Message = "User not found." });
            }

            var transactions = await _context.Transactions
                .Where(t => t.UserId == userId)
                .Include(t => t.Category)
                .ToListAsync();

            var pdf = _pdfReportService.GenerateTransactionReport(transactions, user);

            return File(pdf, "application/pdf", "TransactionReport.pdf");

        }

        [HttpGet("monthly-budgets")]
        public async Task<IActionResult> GetMonthlyBudgetReport()
        {
            var userId = GetParseUserId();
            if (userId == 0)
            {
                return Unauthorized(new { Message = "Error UserId." });
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User not found.");
                return NotFound(new { Message = "User not found." });
            }
            var budgets  = await _context.MonthlyBudgets
                .Where(t => t.UserId == userId)
                .Include(t => t.Category)
                .ToListAsync();

            var pdf = _pdfReportService.GenerateMonthlyBudgetReport(budgets, user);

            return File(pdf, "application/pdf", "MonthlyBudgetReport.pdf");
        }


        [HttpGet("available")]
        public IActionResult GetAvailableReports()
        {
            var reports = new List<ReportInfo>()
            {
                new ReportInfo
                {
                    Name = "Raport transakcji",
                    Description = "Raport pokazuje listę transakcji użytkownika.",
                    Endpoint = "/api/reports/transactions"
                },
                new ReportInfo
                {
                    Name = "Raport miesięcznego budżetu",
                    Description = "Raport pokazuje podsumowanie budżetu miesięcznego.",
                    Endpoint = "/api/reports/monthly-budgets"
                }
            };
            return Ok(reports);
        }
    }
}
