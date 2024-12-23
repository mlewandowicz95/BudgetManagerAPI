using BudgetManagerAPI.Constants;
using BudgetManagerAPI.Data;
using BudgetManagerAPI.DTO;
using BudgetManagerAPI.DTO.Admin;
using BudgetManagerAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BudgetManagerAPI.Controllers
{
    [Authorize(Roles = Roles.Admin)]
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AdminController> _logger;


        public AdminController(AppDbContext context, ILogger<AdminController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers(
            [FromQuery] bool? isActive,
            [FromQuery] IEnumerable<string> roles,
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

                if (roles != null && roles.Any())
                {
                    var allowedRoles = Roles.All.Select(role => role.ToLower()).ToHashSet();

                    var filteredRoles = roles
                        .Select(role => role.ToLower())
                        .Where(role => allowedRoles.Contains(role))
                        .ToHashSet();

                    if (filteredRoles.Count != 0)
                    {
                        query = query.Where(u => filteredRoles.Contains(u.Role.ToLower()));
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching transactions.");
                return StatusCode(500, new { Message = "An error occurred while processing your request." });
            }
        }

        [HttpPatch("{id}/role")]
        public async Task<IActionResult> UpdateUserRole(int id, [FromBody] UpdateRoleDto roleDto)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { Message = "User with ID {id} not found.", id });
            }

            if (!Roles.All.Contains(roleDto.Role))
            {
                return BadRequest(new { Message = "Invalid role specified." });
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

        // POST: api/User

        [HttpPost]
        public async Task<ActionResult<UserResponseDto>> PostUser(UserRequestDto userRequestDto)
        {

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = new User
            {
                Email = userRequestDto.Email,
                PasswordHash = userRequestDto.Password,
                Role = userRequestDto.Role,
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();



            return CreatedAtAction("GetUser", new { id = user.Id }, new UserResponseDto
            {
                Id = user.Id,
                Email = user.Email,
                Role = user.Role,

            });
        }

        // PUT: api/User/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser(int id, [FromBody] UpdateUserDto updateUserDto)
        {
            // Sprawdzenie, czy użytkownik istnieje
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { Message = $"User with ID {id} not found." });
            }

            // Walidacja: Predefiniowani użytkownicy nie mogą być edytowani
            if (id >= 1 && id <= 3)
            {
                return BadRequest(new { Message = "Predefined users cannot be edited." });
            }

            // Aktualizacja pól
            if (!string.IsNullOrEmpty(updateUserDto.Email))
            {
                user.Email = updateUserDto.Email;
            }

            if (!string.IsNullOrEmpty(updateUserDto.Role))
            {
                // Walidacja roli
                if (!Roles.All.Contains(updateUserDto.Role))
                {
                    return BadRequest(new { Message = $"Invalid role. Allowed roles are: {string.Join(", ", Roles.All)}" });
                }

                user.Role = updateUserDto.Role;
            }

            try
            {
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { Message = "Concurrency conflict occurred while updating the user." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating user with ID {Id}.", id);
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

            if (id == GetParseUserId())
            {
                return BadRequest(new { Message = "You cannot deactivate yourself." });
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
            // Walidacja: Sprawdzenie, czy użytkownik istnieje
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { Message = $"User with ID {id} not found." });
            }

            // Walidacja: Predefiniowani użytkownicy nie mogą być usuwani
            if (id >= 1 && id <= 3)
            {
                return BadRequest(new { Message = "Predefined users cannot be deleted." });
            }

            try
            {
                // Usuwanie użytkownika
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation("User with ID {Id} deleted successfully.", id);
                return Ok(new { Message = $"User with ID {id} has been deleted successfully." });
            }
            catch (Exception ex)
            {
                // Obsługa błędów
                _logger.LogError(ex, "An error occurred while trying to delete user with ID {Id}.", id);
                return StatusCode(500, new { Message = "An error occurred while processing your request. Please try again later." });
            }
        }





    }
}
