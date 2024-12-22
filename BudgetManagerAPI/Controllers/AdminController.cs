using BudgetManagerAPI.Constants;
using BudgetManagerAPI.Data;
using BudgetManagerAPI.DTO;
using BudgetManagerAPI.DTO.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BudgetManagerAPI.Controllers
{
    [Authorize(Roles = Roles.Admin)]
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AdminController> _logger;


        public AdminController(AppDbContext context, ILogger<AdminController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers(
            [FromQuery] bool? isActive,
            [FromQuery] string roles = "{wszystkie}",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string sortBy = "email",
            [FromQuery] string sortOrder = "asc")
        {
            var userId = GetParseUserId();
            if (userId == 0)
            {
                _logger.LogError("Error in UserId.");
                return Unauthorized(new { Message = "Error in UserId." });
            }

            // do zastanowienia czy potrzebuje info o naszym uzyytkowniku(adminie) zalogowanym.
            try
            {
                var query = _context.Users.AsQueryable();
                if (isActive != null)
                {
                    if (isActive == true)
                        query = query.Where(u => u.IsActive);
                    else
                        query = query.Where(u => !u.IsActive);
                }


                if (roles != "{wszystkie}")
                {
                    switch (roles.ToLower())
                    {
                        case "admin":
                            query = query.Where(u => u.Role.ToLower() == "admin");
                            break;

                        case "pro":
                            query = query.Where(u => u.Role.ToLower() == "pro");
                            break;

                        case "user":
                            query = query.Where(u => u.Role.ToLower() == "user");
                            break;
                    }
                }

                if (!string.IsNullOrEmpty(sortBy))
                {
                    query = sortBy.ToLower() switch
                    {
                        "role" => sortOrder.Equals("asc", StringComparison.CurrentCultureIgnoreCase) ? query.OrderBy(u => u.Role) : query.OrderByDescending(u => u.Role),
                        "isactive" => sortOrder.Equals("asc", StringComparison.CurrentCultureIgnoreCase) ? query.OrderBy(u => u.IsActive) : query.OrderByDescending(u => u.IsActive),
                        "createdat" => sortOrder.Equals("asc", StringComparison.CurrentCultureIgnoreCase) ? query.OrderBy(u => u.CreatedAt) : query.OrderByDescending(u => u.CreatedAt),
                        "lastlogin" => sortOrder.Equals("asc", StringComparison.CurrentCultureIgnoreCase) ? query.OrderBy(u => u.LastLogin) : query.OrderByDescending(u => u.LastLogin),
                        _ => sortOrder.Equals("asc", StringComparison.CurrentCultureIgnoreCase) ? query.OrderBy(u => u.Email) : query.OrderByDescending(u => u.Email),
                    };
                }


                var totalItems = await query.CountAsync();

                // Przekształcenie wyników do DTO
                var users = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(user => new UserAdminResponseDto
                    {
                        Email = user.Email,
                        Role = user.Role,
                        Id = user.Id,
                        IsActive = user.IsActive,
                        LastLogin = user.LastLogin
                    })
                    .ToListAsync();


                var result = new PagedResult<UserAdminResponseDto>
                {
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalItems = totalItems,
                    TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
                    Items = users
                };

                return Ok(result);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching transactions.");
                return StatusCode(500, new { Message = "An error occurred while processing your request." });
            }
        }

        [HttpPatch("{id}/role")]
        public async Task<IActionResult> UpdateUserRole(int id, [FromBody] UpdateRoleDto roleDto)
        {
            var user = await _context.Users.FindAsync(id);
            if(user == null)
            {
                return NotFound(new { Message = "User with ID {id} not found.", id });
            }

            user.Role = roleDto.Role;

            try
            {
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while update user role.");
                return StatusCode(500, new { Message = "An error occurred while processing your request." });
            }
        }

        [HttpPatch("{id}/change-is-active")]
        public async Task<IActionResult> ChangeIsActive(int id, ChangeActiveUserDto dto)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { Message = "User with ID {id} not found.", id });
            }

            user.IsActive = dto.IsActive;

            try
            {
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while update user active.");
                return StatusCode(500, new { Message = "An error occurred while processing your request." });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { Message = "User with ID {id} not found.", id });
            }

            if (id >= 1 && id <= 3)
            {
                return BadRequest("Prefedined users cannot be deleted from database.");
            }
           

           
            try
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                return Ok("User was deleted.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while delete user active.");
                return StatusCode(500, new { Message = "An error occurred while processing your request." });
            }
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
    }
}
