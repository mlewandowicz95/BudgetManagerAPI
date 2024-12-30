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
        public async Task<IActionResult> GetAllGoals()
        {
            try
            {
                _logger.LogInformation("Fetching all goals.");

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
                     }).ToListAsync();
                _logger.LogInformation("Successfully fetched {Count} goals.", goals.Count());

                return Ok(goals);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occured while fetching goals.");
                return StatusCode(500, new { Message = "An error occured while processing your request." });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> RemoveGoal(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new { Message = "Invalid ID" });
            }

            var goal = await _context.Goals.FindAsync(id);
            if (goal == null)
            {
                return NotFound(new { Message = "Goal with ID {id} not found.", id });
            }

            _context.Goals.Remove(goal);

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Goal with ID {id} deleted successfully.", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while deleting goal with ID {Id}.", id);
                return StatusCode(500, new { Message = "An error occurred while processing your request." });
            }
            return NoContent();
        }
    }
}
