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
    [Route("api/[controller]")]
    [ApiController]
    public class TransactionController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<TransactionController> _logger;


        public TransactionController(AppDbContext context, ILogger<TransactionController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private int GetParseUserId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return 0;
            }


            int parsedUserId = int.Parse(userId);
            return parsedUserId;
        }

        //GET: api/Transaction
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetTransactions(
    [FromQuery] TransactionType? type,      // Przychód/Wydatek
    [FromQuery] int? categoryId,           // ID kategorii
    [FromQuery] DateTime? startDate,       // Początek zakresu dat
    [FromQuery] DateTime? endDate,         // Koniec zakresu dat
    [FromQuery] string? description,       // Opis (częściowy)
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 10,
    [FromQuery] string sortBy = "date",
    [FromQuery] string sortOrder = "desc")        
        {
            try
            {
                _logger.LogInformation("Fetching transactions with filters");

                var query = _context.Transactions.AsQueryable();

                // Filtrowanie według typu transakcji
                if (type.HasValue)
                {
                    query = query.Where(tran => tran.Type == type.Value);
                }

                // Filtrowanie według kategorii
                if (categoryId.HasValue)
                {
                    query = query.Where(tran => tran.CategoryId == categoryId.Value);
                }

                // Filtrowanie według zakresu dat
                if (startDate.HasValue)
                {
                    query = query.Where(tran => tran.Date >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(tran => tran.Date <= endDate.Value);
                }

                // Filtrowanie według opisu
                if (!string.IsNullOrEmpty(description))
                {
                    query = query.Where(tran => tran.Description != null && tran.Description.Contains(description));
                }

                if(!string.IsNullOrEmpty(sortBy))
                {
                    query = sortBy.ToLower() switch
                    {
                        "amount" => sortOrder.Equals("asc", StringComparison.CurrentCultureIgnoreCase) ? query.OrderBy(t => t.Amount) : query.OrderByDescending(t => t.Amount),
                        "category" => sortOrder.Equals("asc", StringComparison.CurrentCultureIgnoreCase) ? query.OrderBy(t => t.Category.Name) : query.OrderByDescending(t => t.Category.Name),
                        _ => sortOrder.Equals("asc", StringComparison.CurrentCultureIgnoreCase) ? query.OrderBy(t => t.Date) : query.OrderByDescending(t => t.Date),
                    };
                }

                var totalItems = await query.CountAsync();

                // Przekształcenie wyników do DTO
                var transactions = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(tran => new TransactionDto
                    {
                        CategoryName = tran.Category.Name,
                        Amount = tran.Amount,
                        Date = tran.Date,
                        Description = tran.Description,
                        Type = tran.Type.ToString(),
                    })
                    .ToListAsync();

                var result = new PagedResult<TransactionDto>
                {
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalItems = totalItems,
                    TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
                    Items = transactions
                };

                _logger.LogInformation("Successfully fetched {Count} transactions with filters", transactions.Count());
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching transactions.");
                return StatusCode(500, new { Message = "An error occurred while processing your request." });
            }
        }

        // GET: api/Transaction/5
        [HttpGet("{id}")]
        public async Task<ActionResult<TransactionResponseDto>> GetTransaction(int id)
        {
            try
            {
                var transaction = await _context.Transactions.FindAsync(id);
                if (transaction == null)
                {
                    return NotFound(new { Message = $"Transaction with ID {id} not found" });
                }

                TransactionResponseDto transactionResponseDto = new TransactionResponseDto
                {
                    Id = transaction.Id,
                    UserId = transaction.UserId,
                    CategoryId = transaction.CategoryId,
                    Amount = transaction.Amount,
                    Date = transaction.Date,
                    Description = transaction.Description,
                    IsRecurring = transaction.IsRecurring,
                    Type = transaction.Type,
                };

                return Ok(transactionResponseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetTransaction(int id): {ex.Message}");
                return StatusCode(500, new { Message = "An error occured while processing your request." });
            }
        }

        // POST: api/Transaction
        [HttpPost]
        public async Task<ActionResult<TransactionResponseDto>> PostTransaction([FromBody] TransactionRequestDto transactionRequestDto)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogError("Model is not valid");
                return BadRequest(ModelState);
            }

            var transaction = new Transaction
            {
                UserId = transactionRequestDto.UserId,
                CategoryId = transactionRequestDto.CategoryId,
                Amount = transactionRequestDto.Amount,
                Date = transactionRequestDto.Date,
                Description = transactionRequestDto.Description,
                IsRecurring = transactionRequestDto.IsRecurring,
                Type = transactionRequestDto.Type
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetTransaction", new { id = transaction.Id }, new TransactionResponseDto
            {
                Id = transaction.Id,
                UserId = transaction.UserId,
                CategoryId = transaction.CategoryId,
                Amount = transaction.Amount,
                Date = transaction.Date,
                Description = transaction.Description,
                IsRecurring = transaction.IsRecurring,
                Type = transaction.Type
            });
        }


        //PUT: api/Transaction/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTransaction(int id, [FromBody] TransactionRequestDto dto)
        {
            if (id <= 0)
            {
                _logger.LogError("Invalid ID");
                return BadRequest(new { Message = "Invalid ID" });
            }

            var transaction = await _context.Transactions.FindAsync(id);
            if (transaction == null)
            {
                return NotFound(new { Message = $"Transaction with ID {id} not found." });
            }

            transaction.UserId = dto.UserId;
            transaction.CategoryId = dto.CategoryId;
            transaction.Amount = dto.Amount;
            transaction.Date = dto.Date;
            transaction.Description = dto.Description;
            transaction.IsRecurring = dto.IsRecurring;
            transaction.Type = dto.Type;

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Transaction with ID {id} updated successfully", id);
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning("Concurrency conflict while updating transaction with ID {id}.", id);
                return StatusCode(409, new { Message = "Concurrency conflict occured while updating the transaction." });
            }
            return NoContent();
        }

        // DELETE: api/Category/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTransaction(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new { Message = "Invalid ID" });
            }

            var transaction = await _context.Transactions.FindAsync(id);
            if (transaction == null)
            {
                _logger.LogWarning("Transaction with ID {Id} not found for deletion.", id);
                return NotFound(new { Message = $"Transaction with ID {id} not found." });
            }

            _context.Transactions.Remove(transaction);
            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Transaction with ID {Id} deleted successfully.", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while deleting transaction with ID {Id}.", id);
                return StatusCode(500, new { Message = "An error occurred while processing your request." });
            }

            return NoContent();
        }
    }
}
