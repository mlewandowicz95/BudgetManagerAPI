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
            if (id <= 0)
            {
                return BadRequest(new ErrorResponseDto
                {
                    Success = false,
                    Message = "Invalid category ID.",
                    ErrorCode = ErrorCodes.InvalidId,
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                _logger.LogWarning("Category with ID {Id} not found for deletion. TraceId: {TraceId}", id, HttpContext.TraceIdentifier);
                return NotFound(new ErrorResponseDto
                {
                    Success = false,
                    Message = $"Category with ID {id} not found.",
                    ErrorCode = ErrorCodes.NotFound,
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            try
            {
                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Category with ID {Id} deleted successfully by admin. TraceId: {TraceId}", id, HttpContext.TraceIdentifier);

                return Ok(new SuccessResponseDto<object>
                {
                    Success = true,
                    Message = $"Category with ID {id} has been deleted successfully.",
                    TraceId = HttpContext.TraceIdentifier,
                    Data = null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while deleting category with ID {Id}. TraceId: {TraceId}", id, HttpContext.TraceIdentifier);

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
