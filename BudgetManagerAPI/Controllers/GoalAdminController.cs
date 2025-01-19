using BudgetManagerAPI.Constants;
using BudgetManagerAPI.Data;
using BudgetManagerAPI.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BudgetManagerAPI.Controllers
{
    [Authorize(Roles = Roles.Admin)]
    [ApiController]
    [Route("api/admin/goals")]
    public class GoalAdminController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly ILogger<GoalAdminController> _logger;

        public GoalAdminController(AppDbContext context, ILogger<GoalAdminController> logger)
        {
            _context = context;
            _logger = logger;
        }


        [HttpGet]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> GetAllGoals()
        {
            try
            {
                _logger.LogInformation("Fetching all goals. TraceId: {TraceId}", HttpContext.TraceIdentifier);

                var goals = await _context.Goals
                    .Select(goal => new GoalResponseDto
                    {
                        Id = goal.Id,
                        UserId = goal.UserId,
                        CreatedAt = goal.CreatedAt,
                        TargetAmount = goal.TargetAmount,
                        DueDate = goal.DueDate,
                        Name = goal.Name,
                        CurrentProgress = goal.CurrentProgress,
                    })
                    .ToListAsync();

                _logger.LogInformation("Successfully fetched {Count} goals. TraceId: {TraceId}", goals.Count, HttpContext.TraceIdentifier);

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
                _logger.LogError(ex, "An error occurred while fetching goals. TraceId: {TraceId}", HttpContext.TraceIdentifier);

                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Message = "An error occurred while processing your request.",
                    ErrorCode = ErrorCodes.InternalServerError,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }


        [HttpDelete("{id}")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> RemoveGoal(int id)
        {
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

            var goal = await _context.Goals.FindAsync(id);
            if (goal == null)
            {
                _logger.LogWarning("Goal with ID {Id} not found for deletion. TraceId: {TraceId}", id, HttpContext.TraceIdentifier);
                return NotFound(new ErrorResponseDto
                {
                    Success = false,
                    Message = $"Goal with ID {id} not found.",
                    ErrorCode = ErrorCodes.NotFound,
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            try
            {
                _context.Goals.Remove(goal);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Goal with ID {Id} deleted successfully. TraceId: {TraceId}", id, HttpContext.TraceIdentifier);

                return Ok(new SuccessResponseDto<object>
                {
                    Success = true,
                    Message = $"Goal with ID {id} has been deleted successfully.",
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
