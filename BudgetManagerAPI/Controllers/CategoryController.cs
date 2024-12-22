using BudgetManagerAPI.Constants;
using BudgetManagerAPI.Data;
using BudgetManagerAPI.DTO;
using BudgetManagerAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace BudgetManagerAPI.Controllers
{
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
        [Authorize]
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                var userId = GetParseUserId();

                IQueryable<Category> query = _context.Categories;

                if (!User.IsInRole(Roles.Admin))
                {
                    // Ograniczenie widoczności dla Pro i User
                    query = query.Where(c => c.UserId == null || c.UserId == userId);
                }

                var categories = await query
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
        [Authorize]
        public async Task<IActionResult> GetCategory(int id)
        {
            try
            {
                var category = await _context.Categories.FindAsync(id);
                if (category == null)
                {
                    return NotFound(new { Message = $"Category with ID {id} not found." });
                }

                if(!User.IsInRole(Roles.Admin) && category.UserId == null)
                {
                    return Forbid("Only admin can see not mine category.");
                }

                CategoryResponseDto categoryResponseDto = new CategoryResponseDto
                {
                    Id = category.Id,
                    Name = category.Name,
                    UserId = category.UserId
                };

                return Ok(categoryResponseDto);
            }
            catch (Exception ex)
            {

                _logger.LogError($"Error in GetCatergory(int id): {ex.Message}");

                return StatusCode(500, "An error occured while processing your request.");
            }
        }


        // POST: api/Category
        [Authorize(Roles = $"{Roles.Admin},{Roles.Pro}")]
        [HttpPost]
        public async Task<ActionResult<Category>> PostCategory([FromBody] CategoryRequestDto categoryRequestDto)
        {
            var userId = GetParseUserId();
            if (userId == 0)
            {
                return Unauthorized(new { Message = "Error user id." });
            }



            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if(User.IsInRole(Roles.Pro) && categoryRequestDto.UserId != categoryRequestDto.UserId)
            {
                return Forbid("Pro users can only create categories for themselves.");
            }

            if(User.IsInRole(Roles.Admin) && categoryRequestDto.UserId == null)
            {
                var globalCategory = new Category
                {
                    Name = categoryRequestDto.Name,
                    UserId = null,
                };

                _context.Categories.Add(globalCategory);
                await _context.SaveChangesAsync();
                return CreatedAtAction("GetCategory", new { id = globalCategory.Id }, new CategoryResponseDto
                {
                    Id = globalCategory.Id,
                    Name = globalCategory.Name,
                    UserId = globalCategory.UserId,
                });
            }


            var category = new Category
            {
                Name = categoryRequestDto.Name,
                UserId = categoryRequestDto.UserId
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetCategory", new { id = category.Id }, new CategoryResponseDto
            {
                Id = category.Id,
                Name = category.Name,
                UserId = category.UserId
            });
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

        // PUT: api/Category/2
        [HttpPut("{id}")]
        [Authorize(Roles = $"{Roles.Admin},{Roles.Pro}")]
        public async Task<IActionResult> PutCategory(int id, [FromBody] CategoryRequestDto dto)
        {
            var userId = GetParseUserId();

            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                return NotFound(new { Message = $"Category with ID {id} not found." });
            }


            if(User.IsInRole(Roles.Admin))
            {
                category.Name = dto.Name;
                category.UserId = dto.UserId;
            }
            else if(category.UserId == userId)
            {
                category.Name = dto.Name;
            }
            else
            {
                return Forbid("You can only edit your own categories.");
            }

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
