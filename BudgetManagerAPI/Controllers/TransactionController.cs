using BudgetManagerAPI.Constants;
using BudgetManagerAPI.Data;
using BudgetManagerAPI.DTO;
using BudgetManagerAPI.Enums;
using BudgetManagerAPI.Models;
using BudgetManagerAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BudgetManagerAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TransactionController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly ILogger<TransactionController> _logger;
        private readonly AlertService _alertService;

        public TransactionController(AppDbContext context, ILogger<TransactionController> logger, AlertService alertService)
        {
            _context = context;
            _logger = logger;
            _alertService = alertService;
        }

        //GET: api/Transaction
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetTransactions(
            [FromQuery] TransactionType? type, // Przychód/Wydatek
            [FromQuery] int? categoryId, // ID kategorii
            [FromQuery] DateTime? startDate, // Początek zakresu dat
            [FromQuery] DateTime? endDate, // Koniec zakresu dat
            [FromQuery] string? description, // Opis (częściowy)
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string sortBy = "date",
            [FromQuery] string sortOrder = "desc")
        {
            try
            {
                var userId = GetParseUserId();
                var userRole = User.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value;
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

                _logger.LogInformation("Fetching transactions with filters for UserId: {UserId}", userId);

                var query = _context.Transactions.AsQueryable();

                
                if (userRole != Roles.Admin)
                {
                    query = query.Where(t => t.UserId == userId);
                } 

                // Filtrowanie według typu transakcji
                if (type.HasValue)
                {
                    query = query.Where(tran => tran.Type == type.Value);
                }

                if (categoryId.HasValue)
                {
                    query = query.Where(tran => tran.CategoryId == categoryId.Value);
                }

                if (startDate.HasValue)
                {
                    query = query.Where(tran => tran.Date.Date >= startDate.Value.Date);
                }

                if (endDate.HasValue)
                {
                    var adjustedEndDate = endDate.Value.Date.AddDays(1).AddSeconds(-1);
                    query = query.Where(tran => tran.Date <= adjustedEndDate);
                }

                if (!string.IsNullOrEmpty(description))
                {
                    query = query.Where(tran => tran.Description != null && tran.Description.Contains(description));
                }

                if (!string.IsNullOrEmpty(sortBy))
                {
                    query = sortBy.ToLower() switch
                    {
                        "amount" => sortOrder.Equals("asc", StringComparison.CurrentCultureIgnoreCase) ? query.OrderBy(t => t.Amount) : query.OrderByDescending(t => t.Amount),
                        "category" => sortOrder.Equals("asc", StringComparison.CurrentCultureIgnoreCase) ? query.OrderBy(t => t.Category.Name) : query.OrderByDescending(t => t.Category.Name),
                        "description" => sortOrder.Equals("asc", StringComparison.CurrentCultureIgnoreCase) ? query.OrderBy(t => t.Description) : query.OrderByDescending(t => t.Description),
                        "type" => sortOrder.Equals("asc", StringComparison.CurrentCultureIgnoreCase) ? query.OrderBy(t => t.Type) : query.OrderByDescending(t => t.Type),
                        _ => sortOrder.Equals("asc", StringComparison.CurrentCultureIgnoreCase) ? query.OrderBy(t => t.Date) : query.OrderByDescending(t => t.Date),
                    };
                }


                var totalItems = await query.CountAsync();

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

                _logger.LogInformation("Successfully fetched {Count} transactions for UserId: {UserId}", transactions.Count, userId);

                // Zwrócenie sukcesu
                return Ok(new SuccessResponseDto<PagedResult<TransactionDto>>
                {
                    Success = true,
                    Message = "Transactions retrieved successfully.",
                    TraceId = HttpContext.TraceIdentifier,
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching transactions. TraceId: {TraceId}", HttpContext.TraceIdentifier);
                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Message = "An error occurred while processing your request. Please try again later.",
                    ErrorCode = ErrorCodes.InternalServerError,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }


        [HttpGet("{id}")]
        public async Task<IActionResult> GetTransaction(int id)
        {
            try
            {
                var userId = GetParseUserId();
                var userRole = User.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value;

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

                if (string.IsNullOrEmpty(userRole))
                {
                    return Unauthorized(new ErrorResponseDto
                    {
                        Success = false,
                        TraceId = HttpContext.TraceIdentifier,
                        Message = "User role is not assigned or invalid.",
                        ErrorCode = ErrorCodes.Unathorized
                    });
                }

                _logger.LogInformation("Fetching transaction with ID {Id} for UserId {UserId} and Role {UserRole}", id, userId, userRole);

                var transaction = await _context.Transactions
                    .Where(t => userRole == Roles.Admin || t.UserId == userId)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (transaction == null)
                {
                    var exists = await _context.Transactions.AnyAsync(t => t.Id == id);
                    var message = exists
                        ? "You do not have permission to view this transaction."
                        : $"Transaction with ID {id} not found.";

                    return NotFound(new SuccessResponseDto<object>
                    {
                        Success = false,
                        Message = message,
                        TraceId = HttpContext.TraceIdentifier,
                        Data = null
                    });
                }

                var transactionResponseDto = new TransactionResponseDto
                {
                    Id = transaction.Id,
                    UserId = transaction.UserId,
                    CategoryId = transaction.CategoryId,
                    GoalId = transaction.GoalId,
                    Amount = transaction.Amount,
                    Date = transaction.Date,
                    Description = transaction.Description,
                    IsRecurring = transaction.IsRecurring,
                    Type = transaction.Type
                };

                _logger.LogInformation("Transaction with ID {Id} successfully retrieved for UserId {UserId}", id, userId);

                return Ok(new SuccessResponseDto<TransactionResponseDto>
                {
                    Data = transactionResponseDto,
                    Success = true,
                    Message = "Transaction retrieved successfully.",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching the transaction. TraceId: {TraceId}", HttpContext.TraceIdentifier);
                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Message = "An error occurred while processing your request. Please try again later.",
                    ErrorCode = ErrorCodes.InternalServerError,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }


        // POST: api/Transaction
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> PostTransaction([FromBody] TransactionRequestDto transactionRequestDto)
        {
            // Walidacja modelu
            if (!ModelState.IsValid)
            {
                var errors = ModelState
    .Where(ms => ms.Value.Errors.Count > 0)
    .ToDictionary(
        kvp => kvp.Key,
        kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
    );


                _logger.LogError("Invalid model state.");
                return BadRequest(new ErrorResponseDto
                {
                    Success = false,
                    Message = "Invalid request data.",
                    TraceId = HttpContext.TraceIdentifier,
                    ErrorCode = ErrorCodes.ValidationError,
                    Errors = errors
                });
            }

            // Pobranie informacji o zalogowanym użytkowniku
            var userId = GetParseUserId();
            var userRole = User.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value;

            if (userId == 0)
            {
                return Unauthorized(new ErrorResponseDto
                {
                    Success = false,
                    Message = "User is not authenticated.",
                    TraceId = HttpContext.TraceIdentifier,
                    ErrorCode = ErrorCodes.Unathorized
                });
            }

            // Sprawdzenie uprawnień
            if (userRole != Roles.Admin && transactionRequestDto.UserId != userId)
            {
                return new ObjectResult(new ErrorResponseDto
                {
                    Success = false,
                    Message = "You do not have permission to add a transaction for another user.",
                    TraceId = HttpContext.TraceIdentifier,
                    ErrorCode = ErrorCodes.Forbidden,
                    Errors = new Dictionary<string, string[]>
                    {
                        { "Authorization", new[]
                            {
                                $"User with ID {userId} attempted to add a transaction for User with ID {transactionRequestDto.UserId}.",
                                "Only Admins can add transactions for other users."
                            }
                        }
                    }
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };

            }

            // Walidacja typu transakcji
            if (transactionRequestDto.Type != TransactionType.Expense && transactionRequestDto.GoalId.HasValue)
            {
                return BadRequest(new ErrorResponseDto
                {
                    Success = false,
                    Message = "Only expense transactions can be linked to a goal.",
                    ErrorCode = ErrorCodes.InvalidData,
                    TraceId = HttpContext.TraceIdentifier
                });
            }


            // Mapowanie danych z DTO
            var transaction = new Transaction
            {
                UserId = transactionRequestDto.UserId,
                CategoryId = transactionRequestDto.CategoryId,
                Amount = transactionRequestDto.Amount,
                Date = transactionRequestDto.Date,
                Description = transactionRequestDto.Description,
                IsRecurring = transactionRequestDto.IsRecurring,
                Type = transactionRequestDto.Type,
                GoalId = transactionRequestDto.GoalId,
            };

            _context.Transactions.Add(transaction);

            if(transaction.GoalId.HasValue )
            {
                var goal = await _context.Goals.FindAsync(transaction.GoalId);
                if(goal != null && goal.UserId == userId)
                {
                    goal.CurrentProgress += transaction.Amount;


                    if(goal.CurrentProgress >= goal.TargetAmount)
                    {
                        goal.CurrentProgress = goal.TargetAmount;

                        await _alertService.CreateAlert(goal.UserId,
    $"Congratulations! You have completed the goal '{goal.Name}'.");
                    }
                }
            }

            await _context.SaveChangesAsync();


            if (transaction.Type == TransactionType.Expense)
            {
                var monthlyBudget = await _context.MonthlyBudgets
                    .FirstOrDefaultAsync(b => b.UserId == transaction.UserId && b.CategoryId == transaction.CategoryId
                        && b.Month == new DateTime(transaction.Date.Year, transaction.Date.Month, 1));

                if (monthlyBudget != null)
                {
                    var totalSpent = await _context.Transactions
                        .Where(t => t.UserId == transaction.UserId && t.CategoryId == transaction.CategoryId
                            && t.Type == transaction.Type && t.Date.Year == transaction.Date.Year && t.Date.Month == transaction.Date.Month)
                        .SumAsync(t => t.Amount);

                    await _context.Entry(transaction).Reference(t => t.Category).LoadAsync();

                    if (totalSpent > monthlyBudget.Amount)
                    {
                        await _alertService.CreateAlert(transaction.UserId,
                            $"You cross budget to category {transaction.Category.Name} o {totalSpent - monthlyBudget.Amount:c}.");
                    }
                    else if (monthlyBudget.Amount - totalSpent < 0.1m * monthlyBudget.Amount)
                    {
                        await _alertService.CreateAlert(transaction.UserId,
                            $"You have less than 10% budget to category {transaction.Category.Name}.");
                    }
                }
            }

            // Zwrócenie odpowiedzi z dodaną transakcją
            return Ok(new SuccessResponseDto<TransactionResponseDto>
            {
                Success = true,
                Message = "Transaction created successfully.",
                TraceId = HttpContext.TraceIdentifier,
                Data = new TransactionResponseDto
                {
                    Id = transaction.Id,
                    UserId = transaction.UserId,
                    CategoryId = transaction.CategoryId,
                    Amount = transaction.Amount,
                    Date = transaction.Date,
                    Description = transaction.Description,
                    IsRecurring = transaction.IsRecurring,
                    Type = transaction.Type,
                    GoalId = transaction.GoalId,
                }
            });
        }



        // PUT: api/Transaction/5
        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> PutTransaction(int id, [FromBody] TransactionRequestDto dto)
        {
            if (id <= 0)
            {
                _logger.LogError("Invalid ID: {Id}", id);
                return BadRequest(new ErrorResponseDto
                {
                    Success = false,
                    Message = "Invalid ID provided.",
                    TraceId = HttpContext.TraceIdentifier,
                    ErrorCode = ErrorCodes.InvalidId,
                    Errors = new Dictionary<string, string[]>
            {
                { "Id", new[] { "ID must be greater than 0." } }
            }
                });
            }

            var userId = GetParseUserId();
            var userRole = User.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value;

            if (userId == 0)
            {
                return Unauthorized(new ErrorResponseDto
                {
                    Success = false,
                    Message = "User is not authenticated.",
                    TraceId = HttpContext.TraceIdentifier,
                    ErrorCode = ErrorCodes.Unathorized
                });
            }

            var transaction = await _context.Transactions.FindAsync(id);
            if (transaction == null)
            {
                return NotFound(new ErrorResponseDto
                {
                    Success = false,
                    Message = $"Transaction with ID {id} not found.",
                    TraceId = HttpContext.TraceIdentifier,
                    ErrorCode = ErrorCodes.NotFound
                });
            }

            // Sprawdzenie uprawnień
            if (userRole != Roles.Admin && transaction.UserId != userId)
            {
                return new ObjectResult(new ErrorResponseDto
                {
                    Success = false,
                    Message = "You do not have permission to edit this transaction.",
                    TraceId = HttpContext.TraceIdentifier,
                    ErrorCode = ErrorCodes.Forbidden,
                    Errors = new Dictionary<string, string[]>
            {
                { "Authorization", new[] { "Editing another user's transaction is not allowed unless you are an Admin." } }
            }
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }

            // Sprawdzenie i ograniczenie zmiany UserId
            if (userRole != Roles.Admin && dto.UserId != transaction.UserId)
            {
                return new ObjectResult(new ErrorResponseDto
                {
                    Success = false,
                    Message = "You do not have permission to change the owner of this transaction.",
                    TraceId = HttpContext.TraceIdentifier,
                    ErrorCode = ErrorCodes.Forbidden,
                    Errors = new Dictionary<string, string[]>
            {
                { "UserId", new[] { "Only Admins can change the owner of a transaction." } }
            }
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }

            // Aktualizacja transakcji
            transaction.UserId = dto.UserId; // Dopuszczalne tylko dla Admina
            transaction.CategoryId = dto.CategoryId;
            transaction.Amount = dto.Amount;
            transaction.Date = dto.Date;
            transaction.Description = dto.Description;
            transaction.IsRecurring = dto.IsRecurring;
            transaction.Type = dto.Type;

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Transaction with ID {Id} updated successfully by UserId {UserId}", id, userId);

                return Ok(new SuccessResponseDto<TransactionResponseDto>
                {
                    Success = true,
                    Message = "Transaction updated successfully.",
                    TraceId = HttpContext.TraceIdentifier,
                    Data = new TransactionResponseDto
                    {
                        Id = transaction.Id,
                        UserId = transaction.UserId,
                        CategoryId = transaction.CategoryId,
                        Amount = transaction.Amount,
                        Date = transaction.Date,
                        Description = transaction.Description,
                        IsRecurring = transaction.IsRecurring,
                        Type = transaction.Type
                    }
                });
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict while updating transaction with ID {Id}.", id);
                return new ObjectResult(new ErrorResponseDto
                {
                    Success = false,
                    Message = "Concurrency conflict occurred while updating the transaction.",
                    TraceId = HttpContext.TraceIdentifier,
                    ErrorCode = ErrorCodes.Conflict,
                    Errors = new Dictionary<string, string[]>
            {
                { "Concurrency", new[] { "Another process has modified this transaction. Please refresh and try again." } }
            }
                })
                {
                    StatusCode = StatusCodes.Status409Conflict
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while updating transaction with ID {Id}.", id);
                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Message = "An unexpected error occurred while processing your request.",
                    TraceId = HttpContext.TraceIdentifier,
                    ErrorCode = ErrorCodes.InternalServerError
                });
            }
        }


        // DELETE: api/Transaction/5
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteTransaction(int id)
        {
            if (id <= 0)
            {
                _logger.LogError("Invalid ID: {Id}", id);
                return BadRequest(new ErrorResponseDto
                {
                    Success = false,
                    Message = "Invalid ID provided.",
                    TraceId = HttpContext.TraceIdentifier,
                    ErrorCode = ErrorCodes.InvalidId,
                    Errors = new Dictionary<string, string[]>
            {
                { "Id", new[] { "ID must be greater than 0." } }
            }
                });
            }

            var userId = GetParseUserId();
            var userRole = User.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value;

            if (userId == 0)
            {
                return Unauthorized(new ErrorResponseDto
                {
                    Success = false,
                    Message = "User is not authenticated.",
                    TraceId = HttpContext.TraceIdentifier,
                    ErrorCode = ErrorCodes.Unathorized
                });
            }

            var transaction = await _context.Transactions.FindAsync(id);
            if (transaction == null)
            {
                _logger.LogWarning("Transaction with ID {Id} not found for deletion.", id);
                return NotFound(new ErrorResponseDto
                {
                    Success = false,
                    Message = $"Transaction with ID {id} not found.",
                    TraceId = HttpContext.TraceIdentifier,
                    ErrorCode = ErrorCodes.NotFound
                });
            }

            // Sprawdzenie uprawnień
            if (userRole != Roles.Admin && transaction.UserId != userId)
            {
                return new ObjectResult(new ErrorResponseDto
                {
                    Success = false,
                    Message = "You do not have permission to delete this transaction.",
                    TraceId = HttpContext.TraceIdentifier,
                    ErrorCode = ErrorCodes.Forbidden,
                    Errors = new Dictionary<string, string[]>
            {
                { "Authorization", new[] { "Deleting another user's transaction is not allowed unless you are an Admin." } }
            }
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }

            // Usunięcie transakcji
            _context.Transactions.Remove(transaction);
            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Transaction with ID {Id} deleted successfully by UserId {UserId}", id, userId);

                return Ok(new SuccessResponseDto<object>
                {
                    Success = true,
                    Message = $"Transaction with ID {id} deleted successfully.",
                    TraceId = HttpContext.TraceIdentifier,
                    Data = null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while deleting transaction with ID {Id}.", id);
                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Message = "An unexpected error occurred while processing your request.",
                    TraceId = HttpContext.TraceIdentifier,
                    ErrorCode = ErrorCodes.InternalServerError
                });
            }
        }

    }
}
