using BudgetManagerAPI.Data;
using BudgetManagerAPI.DTO;
using BudgetManagerAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BudgetManagerAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GoalController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<GoalController> _logger;

        public GoalController(AppDbContext context, ILogger<GoalController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/Goal
        [HttpGet]
        public async Task<ActionResult<IEnumerable<GoalResponseDto>>> GetGoals()
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
                        CurrentProgress = goal.CurrentProgress
                    }).ToListAsync();

                _logger.LogInformation("Successfully fetched {Count} goals.", goals.Count());
                return Ok(goals);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "An error occured while fetching goals.");
                return StatusCode(500, new { Message = "An error occured while processing your request." });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<GoalResponseDto>> GetGoal(int id)
        {
            try
            {
                var goal = await _context.Goals.FindAsync(id);
                if(goal == null)
                {
                    return NotFound(new { Message = $"Goal with ID {id} not found." });
                }

                GoalResponseDto goalResponseDto = new GoalResponseDto
                {
                    Id = goal.Id,
                    UserId = goal.UserId,
                    CreatedAt = goal.CreatedAt,
                    TargetAmount = goal.TargetAmount,
                    DueDate = goal.DueDate,
                    Name = goal.Name,
                    CurrentProgress = goal.CurrentProgress
                };

                return Ok(goalResponseDto);
            }
            catch(Exception ex)
            {
                _logger.LogError($"Error in GetGoal(int id): {ex.Message}");
                return StatusCode(500, new { Message = "An error occured while processing your request." });
            }
        }

        [HttpPost]
        public async Task<ActionResult<GoalResponseDto>> PostGoal([FromBody] GoalRequestDto goalRequestDto)
        {

            if(!ModelState.IsValid)
            {
                _logger.LogError("Model is not valid");
                return BadRequest(ModelState);
            }

            try
            {
                var goal = new Goal
                {
                    UserId = goalRequestDto.UserId,
                    CurrentProgress = goalRequestDto.CurrentProgress,
                    DueDate = goalRequestDto.DueDate,
                    Name = goalRequestDto.Name,
                    TargetAmount = goalRequestDto.TargetAmount,
                    CreatedAt = goalRequestDto.CreatedAt,
                };

                _context.Goals.Add(goal);
                await _context.SaveChangesAsync();

                return CreatedAtAction("GetGoal", new { id = goal.Id }, new GoalResponseDto
                {
                    Id = goal.Id,
                    Name = goal.Name,
                    CurrentProgress = goal.CurrentProgress,
                    DueDate = goalRequestDto.DueDate,
                    CreatedAt = goal.CreatedAt,
                    TargetAmount = goalRequestDto.TargetAmount,
                    UserId = goalRequestDto.UserId
                });
            }
            catch(Exception ex)
            {
                _logger.LogError($"Error in PostGoal: {ex.Message}");
                return StatusCode(500, new { Message = "An error occured while processing your request." });
            }
        }

        // PUT: api/Goal/34
        [HttpPut("{id}")]
        public async Task<IActionResult> PutGoal(int id, [FromBody] GoalRequestDto goalRequestDto)
        {
            if(id <= 0)
            {
                _logger.LogError("Invalid ID");
                return BadRequest(new { Message = "Invalid ID" });
            }

            var goal = await _context.Goals.FindAsync(id);
            if(goal == null)
            {
                return NotFound(new { Message = $"Goal with ID {id} not found." });
            }

            goal.UserId = goalRequestDto.UserId;
            goal.Name = goalRequestDto.Name;
            goal.TargetAmount = goalRequestDto.TargetAmount;
            goal.CurrentProgress = goalRequestDto.CurrentProgress;
            goal.DueDate = goalRequestDto.DueDate;
            goal.CreatedAt = goalRequestDto.CreatedAt;


            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Goal with ID {id} updated successfully.", id);
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning("Concurrency conflict while updating goal with ID {id}.", id);
                return StatusCode(409, new { Message = "Concurrency conflict occured while updating the goal." });
            }
            return NoContent();
        }

        // DELETE: api/Goal/35
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteGoal(int id)
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
