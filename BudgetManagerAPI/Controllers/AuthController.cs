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
using System.Diagnostics;
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
                    ErrorCode = "VALIDATION_ERROR",
                    Errors = errors,
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            if (userDto.ConfirmPassword != userDto.Password)
            {
                _logger.LogError("Passwords do not match. TraceId: {TraceId}", HttpContext.TraceIdentifier);
                return BadRequest(new ErrorResponseDto
                {
                    Success = false,
                    Message = "Passwords do not match.",
                    ErrorCode = "PASSWORDS_MISMATCH",
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            if (await _context.Users.AnyAsync(u => u.Email == userDto.Email))
            {
                _logger.LogError("User with email {email} already exists. TraceId: {TraceId}", userDto.Email, HttpContext.TraceIdentifier);
                return BadRequest(new ErrorResponseDto
                {
                    Success = false,
                    Message = "User already exists.",
                    ErrorCode = "USER_ALREADY_EXISTS",
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(userDto.Password);
            var activationToken = Guid.NewGuid().ToString();

            var user = new User
            {
                Email = userDto.Email,
                PasswordHash = hashedPassword,
                IsActive = false,
                ActivationToken = activationToken
            };

            try
            {
                if (_context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();

                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();

                    var activationLink = Url.Action(
                        nameof(ConfirmEmail),
                        "Auth",
                        new { token = user.ActivationToken },
                        protocol: HttpContext.Request.Scheme);

                    await _emailService.SendEmailAsync(user.Email, "Activate your account",
                        $"Click here to activate your account: {activationLink}");

                    await transaction.CommitAsync();
                }
                else
                {

                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();

                    var activationLink = Url.Action(
                        nameof(ConfirmEmail),
                        "Auth",
                        new { token = user.ActivationToken },
                        protocol: HttpContext.Request.Scheme);

                    await _emailService.SendEmailAsync(user.Email, "Activate your account",
                        $"Click here to activate your account: {activationLink}");
                }
                /*
                return Ok(new UserResponseDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    Role = user.Role,
                }); */
                return Ok(new SuccessResponseDto<UserResponseDto>
                {
                    Success = true,
                    TraceId = HttpContext.TraceIdentifier,
                    Message = "User has been registered.",
                    Data = new UserResponseDto
                    {
                        Id = user.Id,
                        Email = user.Email,
                        Role = user.Role,
                    }
                });


            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while registering the user. TraceId: {TraceId}", HttpContext.TraceIdentifier);
                return StatusCode(500, new ErrorResponseDto
                {

                    Success = false,
                    Message = "An error occurred while processing your request. Please try again later.",
                    ErrorCode = "INTERNAL_SERVER_ERROR",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }

        [HttpGet("confirm-email")]
        public async Task<IActionResult> ConfirmEmail([FromQuery] string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError("Invalid confirmation request. Missing token. TraceId: {TraceId}", HttpContext.TraceIdentifier);
                return BadRequest(new ErrorResponseDto
                {
                    Message = "Invalid confirmation request.",
                    ErrorCode = "MISSING_TOKEN",
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.ActivationToken == token);
                if (user == null)
                {
                    _logger.LogError("Invalid activation token. TraceId: {TraceId}", HttpContext.TraceIdentifier);
                    return NotFound(new ErrorResponseDto
                    {
                        Success = false,
                        Message = "Invalid activation token.",
                        ErrorCode = "INVALID_ACTIVATION_TOKEN",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                if (user.IsActive)
                {
                    _logger.LogInformation("User is already active. Email: {Email}, TraceId: {TraceId}", user.Email, HttpContext.TraceIdentifier);
                    return BadRequest(new ErrorResponseDto
                    {
                        Success = false,
                        Message = "User is already active.",
                        ErrorCode = "USER_ALREADY_ACTIVE",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                user.IsActive = true;
                user.ActivationToken = string.Empty;
                await _context.SaveChangesAsync();

                _logger.LogInformation("User has been activated. Email: {Email}, TraceId: {TraceId}", user.Email, HttpContext.TraceIdentifier);

                return Ok(new SuccessResponseDto<ConfirmEmailResponseDto>
                {
                    Success = true,
                    Message = "Account activated successfully. You can now log in.",
                    TraceId = HttpContext.TraceIdentifier,
                    Data = new ConfirmEmailResponseDto
                    {
                        Email = user.Email
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while confirming the email. TraceId: {TraceId}", HttpContext.TraceIdentifier);

                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponseDto
                {
                    Success = false,
                    Message = "An error occurred while processing your request. Please try again later.",
                    ErrorCode = "INTERNAL_SERVER_ERROR",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }
        [HttpPost("resend-activasion-link")]
        public async Task<IActionResult> ResendActivationLink([FromBody] ResendActivationLinkRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(ms => ms.Value.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                    );

                return BadRequest(new ErrorResponseDto
                {
                    Success = false,
                    Message = "Validation failed.",
                    ErrorCode = "VALIDATION_ERROR",
                    Errors = errors,
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
                if (user == null)
                {
                    return NotFound(new ErrorResponseDto
                    {
                        Success = false,
                        Message = "User not found.",
                        ErrorCode = "USER_NOT_FOUND",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                if (user.IsActive)
                {
                    return BadRequest(new ErrorResponseDto
                    {
                        Success = false,
                        Message = "User is already active.",
                        ErrorCode = "USER_ALREADY_ACTIVE",
                        TraceId = HttpContext.TraceIdentifier
                    });
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

                _logger.LogInformation("Activation link has been resent for email: {Email}. TraceId: {TraceId}", user.Email, HttpContext.TraceIdentifier);

                return Ok(new SuccessResponseDto<ResendActivationLinkResponseDto>
                {
                    Success = true,
                    Message = "Activation link has been resent.",
                    TraceId = HttpContext.TraceIdentifier,
                    Data = new ResendActivationLinkResponseDto { Email = user.Email }
                });
            }
            catch(Exception ex)
            {
                {
                    _logger.LogError(ex, "An unexpected error occurred. TraceId: {TraceId}", HttpContext.TraceIdentifier);
                    return StatusCode(500, new ErrorResponseDto
                    {
                        Success = false,
                        Message = "An error occurred while processing your request. Please try again later.",
                        ErrorCode = "INTERNAL_SERVER_ERROR",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }
            }
            
        }


        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto userDto)
        {
            /*
            if (!ModelState.IsValid)
            {
                _logger.LogError("Model state is not valid");
                return BadRequest(ModelState);
            }
            */
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(ms => ms.Value.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                    );

                _logger.LogError("Model state is not valid.");
                return BadRequest(new ErrorResponseDto
                {
                    Success = false,
                    Message = "Validation failed.",
                    ErrorCode = "VALIDATION_ERROR",
                    Errors = errors,
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userDto.Email);
                if (user == null || !BCrypt.Net.BCrypt.Verify(userDto.Password, user.PasswordHash))
                {
                    _logger.LogError("Invalid email or password.");
                    return Unauthorized(new ErrorResponseDto
                    {
                        Success = false,
                        Message = "Invalid email or password.",
                        ErrorCode = "INVALID_CREDENTIALS",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                if (!user.IsActive)
                {
                    _logger.LogError("Account is not activated.");
                    return Unauthorized(new ErrorResponseDto
                    {
                        Success = false,
                        Message = "Account is not activated.",
                        ErrorCode = "ACCOUNT_NOT_ACTIVATED",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                user.LastLogin = DateTime.UtcNow;

                var token = _tokenService.GenerateToken(user.Id, user.Role);

                await _context.SaveChangesAsync();

                return Ok(new SuccessResponseDto<LoginResponseDto>
                {
                    Success = true,
                    Message = "Login successful.",
                    TraceId = HttpContext.TraceIdentifier,
                    Data = new LoginResponseDto { Token = token }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while logging in. TraceId: {TraceId}", HttpContext.TraceIdentifier);
                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Message = "An error occurred while processing your request. Please try again later.",
                    ErrorCode = "INTERNAL_SERVER_ERROR",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
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
