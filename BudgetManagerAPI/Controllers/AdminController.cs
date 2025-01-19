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
                _logger.LogError("Invalid UserId. TraceId: {TraceId}", HttpContext.TraceIdentifier);
                return Unauthorized(new ErrorResponseDto
                {
                    Message = "Invalid UserId.",
                    Success = false,
                    TraceId = HttpContext.TraceIdentifier,
                    ErrorCode = ErrorCodes.Unathorized
                });
            }

            try
            {
                _logger.LogInformation("Fetching users with parameters: isActive={isActive}, roles={roles}, page={page}, pageSize={pageSize}, sortBy={sortBy}, sortOrder={sortOrder}",
                    isActive, roles, page, pageSize, sortBy, sortOrder);

                var query = _context.Users.AsQueryable();

                // Filter by active status
                if (isActive.HasValue)
                {
                    query = query.Where(u => u.IsActive == isActive.Value);
                }

                // Filter by roles
                if (roles?.Any() == true)
                {
                    var validRoles = roles.Where(role => Roles.All.Contains(role)).ToHashSet();
                    if (validRoles.Any())
                    {
                        query = query.Where(u => validRoles.Contains(u.Role));
                    }
                    else
                    {
                        _logger.LogWarning("None of the provided roles match the allowed roles.");
                    }
                }

                // Sorting
                query = sortBy.ToLower() switch
                {
                    "role" => sortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase) ? query.OrderBy(u => u.Role) : query.OrderByDescending(u => u.Role),
                    "isactive" => sortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase) ? query.OrderBy(u => u.IsActive) : query.OrderByDescending(u => u.IsActive),
                    "createdat" => sortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase) ? query.OrderBy(u => u.CreatedAt) : query.OrderByDescending(u => u.CreatedAt),
                    "lastlogin" => sortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase) ? query.OrderBy(u => u.LastLogin) : query.OrderByDescending(u => u.LastLogin),
                    _ => sortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase) ? query.OrderBy(u => u.Email) : query.OrderByDescending(u => u.Email),
                };

                // Total count
                var totalItems = await query.CountAsync();

                // Pagination
                var users = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(user => new UserAdminResponseDto
                    {
                        Id = user.Id,
                        Email = user.Email,
                        Role = user.Role,
                        IsActive = user.IsActive,
                        CreatedAt = user.CreatedAt,
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

                return Ok(new SuccessResponseDto<PagedResult<UserAdminResponseDto>>
                {
                    Success = true,
                    Message = $"Returned {result.TotalItems} users.",
                    Data = result,
                    TraceId = HttpContext.TraceIdentifier,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching users. TraceId: {TraceId}", HttpContext.TraceIdentifier);
                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Message = "An unexpected error occurred. Please try again later.",
                    ErrorCode = ErrorCodes.InternalServerError,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }


        // POST: api/User

        [HttpPost]
        public async Task<IActionResult> PostUser([FromBody] UserRequestDto userRequestDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ErrorResponseDto
                {
                    Success = false,
                    Message = "Invalid request data.",
                    ErrorCode = ErrorCodes.Unathorized,
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            if (_context.Users.Any(u => u.Email == userRequestDto.Email))
            {
                return Conflict(new ErrorResponseDto
                {
                    Success = false,
                    Message = "Email is already in use.",
                    ErrorCode = ErrorCodes.Conflict,
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            if (!Roles.All.Contains(userRequestDto.Role))
            {
                return BadRequest(new ErrorResponseDto
                {
                    Success = false,
                    Message = $"Invalid role. Allowed roles are: {string.Join(", ", Roles.All)}",
                    ErrorCode = ErrorCodes.InvalidData,
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            var user = new User
            {
                Email = userRequestDto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(userRequestDto.Password),
                Role = userRequestDto.Role,
                CreatedAt = DateTime.UtcNow,
                IsActive = true // Domyślna aktywacja nowego użytkownika
            };

            try
            {
                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                return Ok(new SuccessResponseDto<UserResponseDto>
                {
                    Success = true,
                    Message = "User created successfully.",
                    Data = new UserResponseDto
                    {
                        Id = user.Id,
                        Email = user.Email,
                        Role = user.Role,
                        IsActive= user.IsActive,
                    },
                    TraceId = HttpContext.TraceIdentifier
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating a new user. TraceId: {TraceId}", HttpContext.TraceIdentifier);

                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Message = "An error occurred while processing your request. Please try again later.",
                    ErrorCode = ErrorCodes.InternalServerError,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser(int id, [FromBody] UpdateUserDto updateUserDto)
        {
            if (id <= 0)
            {
                return BadRequest(new ErrorResponseDto
                {
                    Success = false,
                    Message = "Invalid user ID.",
                    ErrorCode = ErrorCodes.Unathorized,
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new ErrorResponseDto
                {
                    Success = false,
                    Message = $"User with ID {id} not found.",
                    ErrorCode = ErrorCodes.NotFound,
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            if (id >= 1 && id <= 3)
            {
                return BadRequest(new ErrorResponseDto
                {
                    Success = false,
                    Message = "Predefined users cannot be edited.",
                    ErrorCode = ErrorCodes.Allowed,
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            // Aktualizacja email
            if (!string.IsNullOrEmpty(updateUserDto.Email))
            {
                user.Email = updateUserDto.Email;
            }

            // Aktualizacja roli
            if (!string.IsNullOrEmpty(updateUserDto.Role))
            {
                if (!Roles.All.Contains(updateUserDto.Role))
                {
                    return BadRequest(new ErrorResponseDto
                    {
                        Success = false,
                        Message = $"Invalid role. Allowed roles are: {string.Join(", ", Roles.All)}",
                        ErrorCode = ErrorCodes.InvalidData,
                        TraceId = HttpContext.TraceIdentifier
                    });
                }
                user.Role = updateUserDto.Role;

            }


            if (updateUserDto.isActive)
            {
                user.IsActive = true;
                user.ActivationToken = string.Empty;
            }
            else
            {
                user.IsActive = false;
            }

            try
            {
                await _context.SaveChangesAsync();

                return Ok(new SuccessResponseDto<UpdateUserResponseDto>
                {
                    Success = true,
                    Message = "User updated successfully.",
                    Data = new UpdateUserResponseDto
                    {
                       Id = user.Id,
                       Email = user.Email,
                       Role = user.Role,
                       IsActive = user.IsActive,
                       CreatedAt = user.CreatedAt,
                       LastLogin = user.LastLogin
                    },
                    TraceId = HttpContext.TraceIdentifier
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new ErrorResponseDto
                {
                    Success = false,
                    Message = "Concurrency conflict occurred while updating the user.",
                    ErrorCode = ErrorCodes.Conflict,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating user with ID {Id}.", id);
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
        public async Task<IActionResult> DeleteUser(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new ErrorResponseDto
                {
                    Success = false,
                    Message = "Invalid user ID.",
                    ErrorCode = ErrorCodes.Unathorized,
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new ErrorResponseDto
                {
                    Success = false,
                    Message = $"User with ID {id} not found.",
                    ErrorCode = ErrorCodes.NotFound,
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            if (id >= 1 && id <= 3)
            {
                return BadRequest(new ErrorResponseDto
                {
                    Success = false,
                    Message = "Predefined users cannot be deleted.",
                    ErrorCode = ErrorCodes.Allowed,
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            try
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation("User with ID {Id} deleted successfully.", id);

                return Ok(new SuccessResponseDto<object>
                {
                    Success = true,
                    Message = $"User with ID {id} has been deleted successfully.",
                    TraceId = HttpContext.TraceIdentifier,
                    Data = null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while trying to delete user with ID {Id}. TraceId: {TraceId}", id, HttpContext.TraceIdentifier);

                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Message = "An error occurred while processing your request. Please try again later.",
                    ErrorCode = ErrorCodes.InternalServerError,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }







    }
}
