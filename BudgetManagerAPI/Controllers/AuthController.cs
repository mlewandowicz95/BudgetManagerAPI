using BudgetManagerAPI.Data;
using BudgetManagerAPI.DTO;
using BudgetManagerAPI.Interfaces;
using BudgetManagerAPI.Models;
using BudgetManagerAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Runtime.Intrinsics.X86;
using System.Security.Claims;

namespace BudgetManagerAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly TokenService _tokenService;
        private readonly ILogger<AuthController> _logger;
        private readonly IEmailService _emailService;

        public AuthController(AppDbContext context, TokenService tokenService, ILogger<AuthController> logger, IEmailService emailService)
        {
            _context = context;
            _tokenService = tokenService;
            _logger = logger;
            _emailService = emailService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] UserRequestDto userDto)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogError("Model state is not valid");
                return BadRequest(ModelState);
            }

            if (userDto.ConfirmPassword != userDto.Password)
            {
                _logger.LogError("Passwords are not the same.");
                return BadRequest(new { Message = "Passwords do not match." });
            }

            if (await _context.Users.AnyAsync(u => u.Email == userDto.Email))
            {
                _logger.LogError("User with email {email} already exists.", userDto.Email);
                return BadRequest(new { Message = "User already exists." });
            }

            // Hashowanie hasła
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(userDto.Password);

            // Generowanie tokenu aktywacyjnego
            var activationToken = Guid.NewGuid().ToString();

            var user = new User
            {
                Email = userDto.Email,
                PasswordHash = hashedPassword,
                IsActive = false,
                ActivationToken = activationToken
            };

            // Zapisanie użytkownika do bazy
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Wysyłanie e-maila aktywacyjnego
            var activationLink = Url.Action(
                nameof(ConfirmEmail),
                "Auth",
                new { token = user.ActivationToken },
                protocol: HttpContext.Request.Scheme);

            await _emailService.SendEmailAsync(user.Email, "Activate your account",
                $"Click here to activate your account: {activationLink}");

            // Zwrócenie odpowiedzi
            return Ok(new UserResponseDto
            {
                Id = user.Id,
                Email = user.Email,
                Role = user.Role,

            });
        }

        [HttpGet("confirm-email")]
        public async Task<IActionResult> ConfirmEmail([FromQuery] string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError("Invalid confirmation request. Missing token.");
                return BadRequest(new { Message = "Invalid confirmation request." });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.ActivationToken == token);
            if (user == null)
            {
                _logger.LogError("Invalid activation token.");
                return NotFound(new { Message = "Invalid activation token." });
            }

            if (user.IsActive)
            {
                _logger.LogInformation("User is already active.");
                return BadRequest(new { Message = "User is already active." });
            }

            user.IsActive = true;
            user.ActivationToken = string.Empty;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"User with email {user.Email} has been activated.");
            return Ok(new { Message = "Account activated successfully. You can now log in." });
        }

        [HttpPost("resend-activasion-link")]
        public async Task<IActionResult> ResendActivationLink([FromBody] ResendActivationRequestDto request)
        {
            if(!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if(user == null)
            {
                return NotFound(new { Message = "User not found." });
            }

            if (user.IsActive)
            {
                return BadRequest(new { Message = "User is already active." });
            }

            user.ActivationToken = Guid.NewGuid().ToString("N");
            await _context.SaveChangesAsync();

            var activationLink = Url.Action(
                nameof(ConfirmEmail),
                "Auth",
                new { token = user.ActivationToken },
                protocol: HttpContext.Request.Scheme
                );

            await _emailService.SendEmailAsync(user.Email, "Activate your account",
                $"Click the link to activate your account: {activationLink}"
                );

            return Ok(new { Message = "Activaion link has been resent." });
        }


        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto userDto)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogError("Model state is not valid");
                return BadRequest(ModelState);
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userDto.Email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(userDto.Password, user.PasswordHash))
            {
                _logger.LogError("Invalid email or password.");
                return Unauthorized(new ErrorResponseDto { Message = "Invalid email or password." });
            }

            if (!user.IsActive)
            {
                _logger.LogError("Account is no activated.");
                return Unauthorized(new ErrorResponseDto { Message = "Account is no activated." });
            }

            user.LastLogin = DateTime.UtcNow;

            var token = _tokenService.GenerateToken(user.Id, user.Email, user.Role);

            await _context.SaveChangesAsync();

            return Ok(new LoginResponseDto
            {
                Token = token,
                User = new UserResponseDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    Role = user.Role
                }
            });
        }

        [HttpPost("request-password-reset")]
        public async Task<IActionResult> RequestPasswordReset([FromBody] ResetPasswordRequestDto requestDto)
        {
            if(!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (string.IsNullOrEmpty(requestDto.Email))
            {
                _logger.LogError("Email is empty");
                return BadRequest(new { Message = "Email is empty" });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == requestDto.Email);
            if (user == null)
            {
                _logger.LogInformation("User with email {email} not found.", requestDto.Email);
                return NotFound(new { Message = "User with email {email} not found.", requestDto.Email });
            }

            var resetToken = Guid.NewGuid().ToString("N");
            user.ResetToken = resetToken;
            user.ResetTokenExpiry = DateTime.UtcNow.AddHours(1);
            await _context.SaveChangesAsync();

            // Wysyłanie e-maila aktywacyjnego
            var activationLink = Url.Action(
                nameof(RequestPasswordReset),
                "Auth",
                new { token = user.ResetToken },
                protocol: HttpContext.Request.Scheme);

            await _emailService.SendEmailAsync(user.Email, "Reset your password",
                $"Use the following link to reset your password: {activationLink}");

            return Ok(new { Message = "Password reset link sent to you email." });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto model)
        {
            if (string.IsNullOrEmpty(model.Token) || string.IsNullOrEmpty(model.NewPassword))
            {
                return BadRequest(new { Message = "Token and new password are required." });
            }
            var user = await _context.Users.FirstOrDefaultAsync(u => u.ResetToken == model.Token);
            if (user == null || string.IsNullOrEmpty(user.ResetToken) || user.ResetTokenExpiry < DateTime.UtcNow)
            {
                return BadRequest(new { Message = "Invalid or expired token." });
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            user.ResetToken = string.Empty;
            user.ResetTokenExpiry = null;
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Password reset successfully." });
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            _logger.LogInformation("Logout action triggered.");

            // Pobranie nagłówka Authorization
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return BadRequest(new { Message = "Authorization header is missing or invalid." });
            }

            // Wyciągnięcie tokena
            var token = authHeader.Replace("Bearer ", "");

            // Pobranie daty wygaśnięcia tokena
            var expiryDate = GetTokenExpiryDate(token);
            if (expiryDate == null)
            {
                return BadRequest(new { Message = "Invalid token." });
            }

            // Sprawdzenie, czy token już wygasł
            if (expiryDate <= DateTime.UtcNow)
            {
                return BadRequest(new { Message = "Token has already expired." });
            }

            try
            {
                // Dodanie tokena do tabeli RevokedTokens
                _context.RevokedTokens.Add(new RevokedToken
                {
                    Token = token,
                    ExpiryDate = expiryDate.Value,
                });

                await _context.SaveChangesAsync();

                return Ok(new { Message = "Logged out successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to revoke token.");
                return StatusCode(500, new { Message = "An error occurred while logging out. Please try again later." });
            }
        }




        private DateTime? GetTokenExpiryDate(string token)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);
                var expClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Exp);

                if (expClaim == null)
                    return null;

                var expUnix = long.Parse(expClaim.Value);
                return DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
            }
            catch (Exception)
            {
                return null; // Jeśli token jest nieprawidłowy
            }
        }

    }
}
