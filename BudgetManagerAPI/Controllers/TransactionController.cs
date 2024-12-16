using BudgetManagerAPI.Data;
using BudgetManagerAPI.DTO;
using BudgetManagerAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        //GET: api/Transaction
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TransactionResponseDto>>> GetTransactions()
        {
            try
            {
                _logger.LogInformation("Fetching all transactions");

                var transactions = await _context.Transactions
                    .Select(tran => new TransactionResponseDto
                    {
                        Id = tran.Id,
                        UserId = tran.UserId,
                        CategoryId = tran.CategoryId,
                        Amount = tran.Amount,
                        Date = tran.Date,
                        Description = tran.Description,
                        IsRecurring = tran.IsRecurring,
                        Type = tran.Type,
                    }).ToListAsync();

                _logger.LogInformation("Successfully fetched {Count} transactions", transactions.Count());
                return Ok(transactions);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "An error occured while fetching transactions.");
                return StatusCode(500, new { Message = "An error occured while processing your request." });
            }
        }

        // GET: api/Transaction/5
        [HttpGet("{id}")]
        public async Task<ActionResult<TransactionResponseDto>> GetTransaction(int id)
        {
            try
            {
                var transaction = await _context.Transactions.FindAsync(id);
                if(transaction == null)
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
            catch(Exception ex)
            {
                _logger.LogError($"Error in GetTransaction(int id): {ex.Message}");
                return StatusCode(500, new { Message = "An error occured while processing your request." });
            }
        }

        // POST: api/Transaction
        [HttpPost]
        public async Task<ActionResult<TransactionResponseDto>> PostTransaction([FromBody] TransactionRequestDto transactionRequestDto)
        {
            if(!ModelState.IsValid)
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
            if(id <= 0)
            {
                _logger.LogError("Invalid ID");
                return BadRequest(new { Message = "Invalid ID" });
            }

            var transaction = await _context.Transactions.FindAsync(id);
            if(transaction == null)
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
