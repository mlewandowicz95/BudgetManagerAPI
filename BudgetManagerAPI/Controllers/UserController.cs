using BudgetManagerAPI.Constants;
using BudgetManagerAPI.Data;
using BudgetManagerAPI.DTO;
using BudgetManagerAPI.Models;
using BudgetManagerAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace BudgetManagerAPI.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly EmailService _emailService;
        private readonly ILogger<UserController> _logger;

        public UserController(AppDbContext context, EmailService emailService, ILogger<UserController> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        // [HttpGet("profile")] – Pobierz dane zalogowanego użytkownika.
        //  [HttpPut("profile/change-password")] – Zmień hasło zalogowanego użytkownika.
        //  [HttpPut("profile/email")] – Zmień hasło zalogowanego użytkownika.


        // GET: api/User/5
        [HttpGet("profile")]
        public async Task<ActionResult<UserResponseDto>> GetUserProfile()
        {
            var userId = GetParseUserId();

            if (userId == 0)
            {
                _logger.LogWarning("Invalid user ID in GetUserProfile.");
                return Unauthorized(new { Message = "Invalid UserId." });
            }

            try
            {
                var user = await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => new UserResponseDto
            {
                Id = u.Id,
                Email = u.Email,
                Role = u.Role
            })
            .FirstOrDefaultAsync();

                if (user == null)
                {
                    _logger.LogWarning("User with ID {UserId} not found.", userId);
                    return NotFound(new { Message = "User not found." });
                }

                return Ok(user);
            }
            catch (Exception ex)
            {

                _logger.LogError("Error in GetUser(int id): {ex.Message}", ex.Message);
                return StatusCode(500, new { Message = "An error occured while processing your request" });
            }
        }


        [HttpPost("profile/change-password")]
        public async Task<IActionResult> ChangePassword(ChangePasswordRequestDto changePasswordRequestDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { Message = "User is not authorized." });
            }

            var user = await _context.Users.FindAsync(int.Parse(userId));
            if (user == null)
            {
                return NotFound(new { Message = "User not found." });
            }

            var isPasswordValid = BCrypt.Net.BCrypt.Verify(changePasswordRequestDto.CurrentPassword, user.PasswordHash);
            if (!isPasswordValid)
            {
                return BadRequest(new { Message = "Current password is incorrect." });
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(changePasswordRequestDto.NewPassword);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Password has been changed successfully." });

        }

        [HttpPut("profile/email")]
        public async Task<IActionResult> RequestEmailChange([FromBody] UpdateEmailDto dto)
        {
            var userId = GetParseUserId();


            if (userId == 0)
            {
                _logger.LogWarning("Invalid user ID in RequestEmailChange.");
                return Unauthorized(new { Message = "Invalid UserId." });
            }


            var emailExistsInDb = await _context.Users.FirstOrDefaultAsync(e => e.Email == dto.NewEmail);
            if(emailExistsInDb != null)
            {
                return BadRequest(new { Message = "Email is already exists in database." });
            }


            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound(new { Message = "User not found." });
            }

            

            try
            {
                var token = Guid.NewGuid().ToString("N");
                user.EmailChangeToken = token;
                user.NewEmail = dto.NewEmail;
                user.EmailChangeTokenExpiry = DateTime.UtcNow.AddHours(24);

                await _context.SaveChangesAsync();

                var confirmationLink = Url.Action(
                    nameof(RequestEmailChange),
                    "Auth",
                    new { token = user.EmailChangeToken },
                    protocol: HttpContext.Request.Scheme);



                await _emailService.SendEmailAsync(dto.NewEmail, "Confirm Email Change",
                     $"Click the link to confirm your email change: {confirmationLink}");

                return Ok(new { Message = "Confirmation email sent." });
            }
            catch(Exception ex)
            {

                _logger.LogError("Error in RequestEmailChange(UpdateEmailDto dto): {ex.Message}", ex.Message);
                return StatusCode(500, new { Message = "An error occured while processing your request" });
            }

        }

        [HttpGet("profile/email/confirm")]
        public async Task<IActionResult> ConfirmEmailChange([FromQuery] string token)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.EmailChangeToken == token);

            if (user == null || user.EmailChangeTokenExpiry < DateTime.UtcNow)
            {
                return BadRequest(new { Message = "Invalid or expired token." });
            }

            try
            {

                user.Email = user.NewEmail;
                user.NewEmail = string.Empty;
                user.EmailChangeToken = string.Empty;
                user.EmailChangeTokenExpiry = null;

                await _context.SaveChangesAsync();

                return Ok(new { Message = "Email address updated successfully." });
            }
            catch (Exception ex)
            {

                _logger.LogError("Error in ConfirmEmailChange(string token): {ex.Message}", ex.Message);
                return StatusCode(500, new { Message = "An error occured while processing your request" });
            }

        }
    }
}
