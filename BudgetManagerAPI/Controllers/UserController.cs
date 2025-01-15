using BudgetManagerAPI.Constants;
using BudgetManagerAPI.Data;
using BudgetManagerAPI.DTO;
using BudgetManagerAPI.Interfaces;
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
        private readonly IEmailService _emailService;
        private readonly ILogger<UserController> _logger;

        public UserController(AppDbContext context, IEmailService emailService, ILogger<UserController> logger)
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
                return Unauthorized(new ErrorResponseDto
                { 
                    Success = false,
                    Message = "Invalid UserId.",
                    TraceId = HttpContext.TraceIdentifier,
                    ErrorCode = ErrorCodes.Unathorized
                });
            }

            try
            {


                var user = await _context.Users.FindAsync(userId);


                if (user == null)
                {
                    _logger.LogWarning("User with ID {UserId} not found.", userId);
                    return NotFound(new ErrorResponseDto 
                    {
                        Success = false,
                        Message = "User not found.",
                        TraceId = HttpContext.TraceIdentifier,
                        ErrorCode = ErrorCodes.UserNotFound
                    });
                }

                return Ok(new SuccessResponseDto<UserResponseDto>
                {
                    Success = true,
                    Message = "Fetching logged user.",
                    TraceId = HttpContext.TraceIdentifier,
                    Data = new UserResponseDto
                    {
                        Email = user.Email,
                        Id = userId,
                        Role = user.Role,
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching transactions. TraceId: {TraceId}", HttpContext.TraceIdentifier);
                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Message = "An error occurred while processing your request. Please try again later.",
                    ErrorCode = ErrorCodes.InternalServerError,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }


        [HttpPost("profile/change-password")]
        public async Task<IActionResult> ChangePassword(ChangePasswordRequestDto changePasswordRequestDto)
        {
            if (!ModelState.IsValid)
            {

                var errors = ModelState
    .Where(ms => ms.Value.Errors.Count > 0)
    .ToDictionary(
        kvp => kvp.Key,
        kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
    );

                _logger.LogError("Model state is not valid. TraceId: {TraceId}", HttpContext.TraceIdentifier);
                return BadRequest(new ErrorResponseDto
                {
                    Success = false,
                    Message = "Validation failed.",
                    ErrorCode = ErrorCodes.ValidationError,
                    Errors = errors,
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            try
            {
                var userId = GetParseUserId();
                if (userId <= 0)
                {
                    _logger.LogWarning("Invalid user ID in GetUserProfile.");
                    return Unauthorized(new ErrorResponseDto
                    {
                        Success = false,
                        Message = "Invalid UserId.",
                        TraceId = HttpContext.TraceIdentifier,
                        ErrorCode = ErrorCodes.Unathorized
                    });
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User with ID {UserId} not found.", userId);
                    return NotFound(new ErrorResponseDto
                    {
                        Success = false,
                        Message = "User not found.",
                        TraceId = HttpContext.TraceIdentifier,
                        ErrorCode = ErrorCodes.UserNotFound
                    });
                }

                var isPasswordValid = BCrypt.Net.BCrypt.Verify(changePasswordRequestDto.CurrentPassword, user.PasswordHash);
                if (!isPasswordValid)
                {
                    return BadRequest(new ErrorResponseDto
                    {
                        Success = false,
                        TraceId = HttpContext.TraceIdentifier,
                        Message = "Current password is incorrect.",
                        ErrorCode = ErrorCodes.InvalidCredentials
                    });

                }

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(changePasswordRequestDto.NewPassword);
                await _context.SaveChangesAsync();

                return Ok(new SuccessResponseDto<object>
                {
                    Success = true,
                    TraceId = HttpContext.TraceIdentifier,
                    Data = null,
                    Message = "Password has been changed successfully."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while ChangePassword. TraceId: {TraceId}", HttpContext.TraceIdentifier);
                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Message = "An error occurred while processing your request. Please try again later.",
                    ErrorCode = ErrorCodes.InternalServerError,
                    TraceId = HttpContext.TraceIdentifier
                });
            }


        }

        [HttpPut("profile/email")]
        public async Task<IActionResult> RequestEmailChange([FromBody] UpdateEmailDto dto)
        {
            var userId = GetParseUserId();
            if (userId <= 0)
            {
                _logger.LogWarning("Invalid user ID in GetUserProfile.");
                return Unauthorized(new ErrorResponseDto
                {
                    Success = false,
                    Message = "Invalid UserId.",
                    TraceId = HttpContext.TraceIdentifier,
                    ErrorCode = ErrorCodes.Unathorized
                });
            }

            var emailExistsInDb = await _context.Users.FirstOrDefaultAsync(e => e.Email == dto.NewEmail);
            if(emailExistsInDb != null)
            {
                return BadRequest(new ErrorResponseDto
                { 
                    Success = false,
                    TraceId= HttpContext.TraceIdentifier,
                    Message = "Email is already exists in database.",
                    ErrorCode = ErrorCodes.Conflict
                });
            }


            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User with ID {UserId} not found.", userId);
                return NotFound(new ErrorResponseDto
                {
                    Success = false,
                    Message = "User not found.",
                    TraceId = HttpContext.TraceIdentifier,
                    ErrorCode = ErrorCodes.UserNotFound
                });
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

                return Ok(new SuccessResponseDto<string>
                {
                    Success = true,
                    TraceId = HttpContext.TraceIdentifier,
                    Data = dto.NewEmail,
                    Message = "Confirmation email sent."
                });

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while RequestEmailChange TraceId: {TraceId}", HttpContext.TraceIdentifier);
                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Message = "An error occurred while processing your request. Please try again later.",
                    ErrorCode = ErrorCodes.InternalServerError,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }

        [HttpGet("profile/email/confirm")]
        public async Task<IActionResult> ConfirmEmailChange([FromQuery] string token)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.EmailChangeToken == token);

            if (user == null || user.EmailChangeTokenExpiry < DateTime.UtcNow)
            {
                return BadRequest(new ErrorResponseDto
                { 
                    Success = false,
                    Message = "Invalid or expired token.",
                    ErrorCode = ErrorCodes.InvalidToken,
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            try
            {

                user.Email = user.NewEmail;
                user.NewEmail = string.Empty;
                user.EmailChangeToken = string.Empty;
                user.EmailChangeTokenExpiry = null;

                await _context.SaveChangesAsync();

                return Ok(new SuccessResponseDto<object>
                { 
                    Success = true,
                    Message = "Email address updated successfully.",
                    TraceId = HttpContext.TraceIdentifier,
                    Data = null
                });
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, "An error occurred while ConfirmEmailChange TraceId: {TraceId}", HttpContext.TraceIdentifier);
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
