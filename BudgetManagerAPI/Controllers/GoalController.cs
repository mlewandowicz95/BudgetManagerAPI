using BudgetManagerAPI.Constants;
using BudgetManagerAPI.Data;
using BudgetManagerAPI.DTO;
using BudgetManagerAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BudgetManagerAPI.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class GoalController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly ILogger<GoalController> _logger;

        public GoalController(AppDbContext context, ILogger<GoalController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetUserGoals()
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
                _logger.LogInformation("Fetching goals for user {UserId}. TraceId: {TraceId}", userId, HttpContext.TraceIdentifier);

                var goals = await _context.Goals
                    .Where(g => g.UserId == userId)
                    .Select(goal => new GoalResponseDto
                    {
                        Id = goal.Id,
                        UserId = goal.UserId,
                        CreatedAt = goal.CreatedAt,
                        TargetAmount = goal.TargetAmount,
                        DueDate = goal.DueDate,
                        Name = goal.Name,
                        CurrentProgress = goal.CurrentProgress
                    })
                    .ToListAsync();

                _logger.LogInformation("Successfully fetched {Count} goals for user {UserId}. TraceId: {TraceId}", goals.Count, userId, HttpContext.TraceIdentifier);

                return Ok(new SuccessResponseDto<List<GoalResponseDto>>
                {
                    Success = true,
                    Message = $"Successfully fetched {goals.Count} goals.",
                    Data = goals,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching goals for user {UserId}. TraceId: {TraceId}", userId, HttpContext.TraceIdentifier);

                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Message = "An error occurred while processing your request.",
                    ErrorCode = ErrorCodes.InternalServerError,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }


        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetGoal(int id)
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

            if (id <= 0)
            {
                return BadRequest(new ErrorResponseDto
                {
                    Success = false,
                    Message = "Invalid goal ID.",
                    ErrorCode = ErrorCodes.InvalidId,
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            try
            {
                var goal = await _context.Goals.FindAsync(id);
                if (goal == null)
                {
                    _logger.LogWarning("Goal with ID {Id} not found. TraceId: {TraceId}", id, HttpContext.TraceIdentifier);
                    return NotFound(new ErrorResponseDto
                    {
                        Success = false,
                        Message = $"Goal with ID {id} not found.",
                        ErrorCode = ErrorCodes.NotFound,
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                if (goal.UserId != userId)
                {
                    _logger.LogWarning("User {UserId} attempted unauthorized access to goal with ID {GoalId}. TraceId: {TraceId}", userId, id, HttpContext.TraceIdentifier);
                    return new ObjectResult(new ErrorResponseDto
                    {
                        Success = false,
                        Message = "You are not authorized to access this goal.",
                        ErrorCode = ErrorCodes.Forbidden,
                        TraceId = HttpContext.TraceIdentifier,
                        Errors = new Dictionary<string, string[]>
                {
                    { "Authorization", new[] { $"User with ID {userId} attempted to access goal with ID {id}." } }
                }
                    })
                    {
                        StatusCode = StatusCodes.Status403Forbidden
                    };
                }

                var goalResponseDto = new GoalResponseDto
                {
                    Id = goal.Id,
                    UserId = goal.UserId,
                    CreatedAt = goal.CreatedAt,
                    TargetAmount = goal.TargetAmount,
                    DueDate = goal.DueDate,
                    Name = goal.Name,
                    CurrentProgress = goal.CurrentProgress
                };

                _logger.LogInformation("Goal with ID {Id} fetched successfully for user {UserId}. TraceId: {TraceId}", id, userId, HttpContext.TraceIdentifier);

                return Ok(new SuccessResponseDto<GoalResponseDto>
                {
                    Success = true,
                    Message = "Goal fetched successfully.",
                    Data = goalResponseDto,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching goal with ID {Id}. TraceId: {TraceId}", id, HttpContext.TraceIdentifier);

                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Message = "An error occurred while processing your request.",
                    ErrorCode = ErrorCodes.InternalServerError,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> PostGoal([FromBody] GoalRequestDto goalRequestDto)
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
                var goal = new Goal
                {
                    UserId = userId,
                    CurrentProgress = goalRequestDto.CurrentProgress,
                    DueDate = goalRequestDto.DueDate,
                    Name = goalRequestDto.Name,
                    TargetAmount = goalRequestDto.TargetAmount,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Goals.Add(goal);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Goal created successfully for user {UserId}. Goal ID: {GoalId}. TraceId: {TraceId}", userId, goal.Id, HttpContext.TraceIdentifier);

                return CreatedAtAction("GetGoal", new { id = goal.Id }, new SuccessResponseDto<GoalResponseDto>
                {
                    Success = true,
                    Message = "Goal created successfully.",
                    Data = new GoalResponseDto
                    {
                        Id = goal.Id,
                        Name = goal.Name,
                        CurrentProgress = goal.CurrentProgress,
                        DueDate = goal.DueDate,
                        CreatedAt = goal.CreatedAt,
                        TargetAmount = goal.TargetAmount,
                        UserId = goal.UserId
                    },
                    TraceId = HttpContext.TraceIdentifier
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating a goal for user {UserId}. TraceId: {TraceId}", userId, HttpContext.TraceIdentifier);

                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Message = "An error occurred while processing your request.",
                    ErrorCode = ErrorCodes.InternalServerError,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }


        // PUT: api/Goal/34
        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> UpdateGoal(int id, [FromBody] GoalRequestDto goalRequestDto)
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

            if (id <= 0)
            {
                _logger.LogError("Invalid goal ID. TraceId: {TraceId}", HttpContext.TraceIdentifier);
                return BadRequest(new ErrorResponseDto
                {
                    Success = false,
                    Message = "Invalid goal ID.",
                    ErrorCode = ErrorCodes.InvalidId,
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            if (!ModelState.IsValid)
            {
                _logger.LogError("Invalid model state for goal update. TraceId: {TraceId}", HttpContext.TraceIdentifier);
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

            var goal = await _context.Goals.FindAsync(id);
            if (goal == null)
            {
                _logger.LogWarning("Goal with ID {Id} not found. TraceId: {TraceId}", id, HttpContext.TraceIdentifier);
                return NotFound(new ErrorResponseDto
                {
                    Success = false,
                    Message = $"Goal with ID {id} not found.",
                    ErrorCode = ErrorCodes.NotFound,
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            if (goal.UserId != userId)
            {
                _logger.LogWarning("Unauthorized attempt by user {UserId} to edit goal {GoalId}. TraceId: {TraceId}", userId, id, HttpContext.TraceIdentifier);
                return new ObjectResult(new ErrorResponseDto
                {
                    Success = false,
                    Message = "You are not authorized to edit this goal.",
                    ErrorCode = ErrorCodes.Forbidden,
                    TraceId = HttpContext.TraceIdentifier,
                    Errors = new Dictionary<string, string[]>
            {
                { "Authorization", new[] { $"User with ID {userId} attempted to edit goal with ID {id}." } }
            }
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }

            // Aktualizacja danych celu
            goal.Name = goalRequestDto.Name;
            goal.TargetAmount = goalRequestDto.TargetAmount;
            goal.CurrentProgress = goalRequestDto.CurrentProgress;
            goal.DueDate = goalRequestDto.DueDate;

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Goal with ID {Id} updated successfully for user {UserId}. TraceId: {TraceId}", id, userId, HttpContext.TraceIdentifier);

                return Ok(new SuccessResponseDto<GoalResponseDto>
                {
                    Success = true,
                    Message = $"Goal with ID {id} updated successfully.",
                    TraceId = HttpContext.TraceIdentifier,
                    Data = new GoalResponseDto
                    {
                        Id = goal.Id,
                        CreatedAt = goal.CreatedAt,
                        TargetAmount = goal.TargetAmount,
                        CurrentProgress = goal.CurrentProgress,
                        DueDate = goal.DueDate,
                        Name = goalRequestDto.Name,
                        UserId = goalRequestDto.UserId,
                    }
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning("Concurrency conflict while updating goal with ID {Id}. TraceId: {TraceId}", id, HttpContext.TraceIdentifier);
                return Conflict(new ErrorResponseDto
                {
                    Success = false,
                    Message = "Concurrency conflict occurred while updating the goal.",
                    ErrorCode = ErrorCodes.Conflict,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating goal with ID {Id}. TraceId: {TraceId}", id, HttpContext.TraceIdentifier);
                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Message = "An error occurred while processing your request.",
                    ErrorCode = ErrorCodes.InternalServerError,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }


        // DELETE: api/Goal/35
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> RemoveGoal(int id)
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

            if (id <= 0)
            {
                _logger.LogError("Invalid goal ID. TraceId: {TraceId}", HttpContext.TraceIdentifier);
                return BadRequest(new ErrorResponseDto
                {
                    Success = false,
                    Message = "Invalid goal ID.",
                    ErrorCode = ErrorCodes.InvalidId,
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            var goal = await _context.Goals.FindAsync(id);
            if (goal == null)
            {
                _logger.LogWarning("Goal with ID {Id} not found. TraceId: {TraceId}", id, HttpContext.TraceIdentifier);
                return NotFound(new ErrorResponseDto
                {
                    Success = false,
                    Message = $"Goal with ID {id} not found.",
                    ErrorCode = ErrorCodes.NotFound,
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            bool hasTransactions = await _context.Transactions.AnyAsync(t => t.GoalId == id);
            if(hasTransactions)
            {
                _logger.LogWarning($"Can't delete goal {goal.Id} with connecting transactions. At the first delete transactions in relation.");
                return BadRequest(new ErrorResponseDto
                {
                    Success = false,
                    Message = "Can't delete goal with connecting transactions. At the first delete transactions in relation.",
                    ErrorCode = ErrorCodes.Allowed,
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            if (goal.UserId != userId)
            {
                _logger.LogWarning("Unauthorized delete attempt by user {UserId} for goal {GoalId}. TraceId: {TraceId}", userId, id, HttpContext.TraceIdentifier);
                return new ObjectResult(new ErrorResponseDto
                {
                    Success = false,
                    Message = "You are not authorized to delete this goal.",
                    ErrorCode = ErrorCodes.Forbidden,
                    TraceId = HttpContext.TraceIdentifier,
                    Errors = new Dictionary<string, string[]>
            {
                { "Authorization", new[] { $"User with ID {userId} attempted to delete goal with ID {id}." } }
            }
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }

            try
            {
                _context.Goals.Remove(goal);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Goal with ID {Id} deleted successfully for user {UserId}. TraceId: {TraceId}", id, userId, HttpContext.TraceIdentifier);

                return Ok(new SuccessResponseDto<object>
                {
                    Success = true,
                    Message = $"Goal with ID {id} deleted successfully.",
                    TraceId = HttpContext.TraceIdentifier,
                    Data = null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while deleting goal with ID {Id}. TraceId: {TraceId}", id, HttpContext.TraceIdentifier);

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
