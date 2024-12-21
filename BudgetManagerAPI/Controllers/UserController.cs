using BudgetManagerAPI.Data;
using BudgetManagerAPI.DTO;
using BudgetManagerAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BudgetManagerAPI.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<UserController> _logger;

        public UserController(AppDbContext context, ILogger<UserController> logger)
        {
            _context = context;
            _logger = logger;
        }


        // GET: api/User
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserResponseDto>>> GetUsers()
        {
            try
            {
                _logger.LogInformation("Fetching all users");

                var users = await _context.Users
                    .Select(user => new UserResponseDto
                    {
                        Id = user.Id,
                        Email = user.Email,
                        Role = user.Role,
                    })
                    .ToListAsync();

                _logger.LogInformation($"Succesufully fetch {users.Count} users");
                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occured while fetching users.");
                return StatusCode(500, new { Message = "An error occured while processing your request." });
            }
        }

        // GET: api/User/5
        [HttpGet("{id}")]
        public async Task<ActionResult<UserResponseDto>> GetUser(int id)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new {Message = $"User with ID {id} not found"});
                }

                UserResponseDto userResponseDto = new UserResponseDto()
                {
                    Id = user.Id,
                    Email = user.Email,
                    Role = user.Role,

                };

                return Ok(userResponseDto);
            }
            catch(Exception ex)
            {

                _logger.LogError($"Error in GetUser(int id): {ex.Message}");
                return StatusCode(500, "An error occured while processing your request");
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
            };



            _context.Users.Add(user);
            await _context.SaveChangesAsync();



            return CreatedAtAction("GetUser", new {id = user.Id}, new UserResponseDto
            {
                Id=user.Id,
                Email= user.Email,
                Role = user.Role,

            });
        }

        // PUT: api/User/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser(int id, [FromBody] UserRequestDto userRequestDto)
        {
            if(id <= 0)
            {
                return BadRequest(new { Message = "Invalid ID." });
            }
            var user = await _context.Users.FindAsync(id);
            if(user == null)
            {
                return NotFound(new { Message = $"User with ID {id} not found" });
            }


            _context.Entry(user).State = EntityState.Modified;
            user.Email = userRequestDto.Email;
            user.PasswordHash = userRequestDto.Password;
            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("User with ID {Id} updated successfully.", id);

            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning("Concurrency conflict while updating user with ID {Id}.", id);
                return StatusCode(409, new { Message = "Concurrency conflict occurred while updating the user." });
            }
            return NoContent();
        }

        //DELETE: api/User/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new { Message = "Invalid ID" });
            }

            var user = await _context.Users.FindAsync(id);
            if(user == null)
            {
                _logger.LogWarning($"User with ID {id} not found for deletion");
                return NotFound(new { Message = $"User with ID {id} not found" });
            }

            _context.Users.Remove(user);

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("User with ID {id} deleted successfully", id);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "An error occured while deleting user with ID {id}", id);
                return StatusCode(500, new { Message = "An error occured while processing your request" });
            }
            return NoContent();
        }
    }
}
