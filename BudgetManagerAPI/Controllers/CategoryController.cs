using BudgetManagerAPI.Data;
using BudgetManagerAPI.DTO;
using BudgetManagerAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace BudgetManagerAPI.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class CategoryController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CategoryController> _logger;

        public CategoryController(AppDbContext context, ILogger<CategoryController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/Category
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CategoryResponseDto>>> GetCategories()
        {
            try
            {
                _logger.LogInformation("Fetching all categories.");

                // Pobranie kategorii i mapowanie na DTO
                var categories = await _context.Categories
                    .Select(category => new CategoryResponseDto
                    {
                        Id = category.Id,
                        Name = category.Name,
                        UserId = category.UserId
                    })
                    .ToListAsync();

                _logger.LogInformation("Successfully fetched {Count} categories.", categories.Count);

                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching categories.");
                return StatusCode(500, new { Message = "An error occurred while processing your request." });
            }
        }


        // GET: api/Category/2
        [HttpGet("{id}")]
        public async Task<ActionResult<CategoryResponseDto>> GetCategory(int id)
        {
            try
            {
                var category = await _context.Categories.FindAsync(id);
                if(category == null)
                {
                    return NotFound(new { Message = $"Category with ID {id} not found." });
                }

                CategoryResponseDto categoryResponseDto = new CategoryResponseDto
                {
                    Id = category.Id,
                    Name = category.Name,
                    UserId = category.UserId
                };

                return Ok(categoryResponseDto);
            }
            catch(Exception ex)
            {

                _logger.LogError($"Error in GetCatergory(int id): {ex.Message}");

                return StatusCode(500, "An error occured while processing your request.");
            }
        }

        // POST: api/Category
        [Authorize(Roles = "Admin,Pro")]
        [HttpPost]
        public async Task<ActionResult<Category>> PostCategory([FromBody] CategoryRequestDto categoryRequestDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var category = new Category
            {
                Name = categoryRequestDto.Name,
                UserId = categoryRequestDto.UserId
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetCategory", new {id = category.Id}, new CategoryResponseDto
            {
                Id = category.Id,
                Name = category.Name,
                UserId =category.UserId
            });
        }
        // PUT: api/Category/2
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Pro")]
        public async Task<IActionResult> PutCategory(int id, [FromBody] CategoryRequestDto dto)
        {

            if (id <= 0)
            {
                return BadRequest(new { Message = "Invalid ID." });
            }


            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                return NotFound(new { Message = $"Category with ID {id} not found." });
            }


            category.Name = dto.Name;
            category.UserId = dto.UserId;

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Category with ID {Id} updated successfully.", id);
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning("Concurrency conflict while updating category with ID {Id}.", id);
                return StatusCode(409, new { Message = "Concurrency conflict occurred while updating the category." });
            }

            return NoContent();
        }


        // DELETE: api/Category/2
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new { Message = "Invalid ID." });
            }

            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                _logger.LogWarning("Category with ID {Id} not found for deletion.", id);
                return NotFound(new { Message = $"Category with ID {id} not found." });
            }

            _context.Categories.Remove(category);

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Category with ID {Id} deleted successfully.", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while deleting category with ID {Id}.", id);
                return StatusCode(500, new { Message = "An error occurred while processing your request." });
            }

            return NoContent();
        }


    }
}
