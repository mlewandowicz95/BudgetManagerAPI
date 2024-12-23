using BudgetManagerAPI.Constants;
using BudgetManagerAPI.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BudgetManagerAPI.Controllers
{
    [Authorize(Roles = Roles.Admin)]
    [ApiController]
    [Route("api/admin/categories")]
    public class CategoryAdminController : BaseController
    {

        private readonly AppDbContext _context;
        private readonly ILogger<AdminController> _logger;


        public CategoryAdminController(AppDbContext context, ILogger<AdminController> logger)
        {
            _context = context;
            _logger = logger;
        }




        // DELETE: api/Category/2
        [HttpDelete("{id}")]
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
