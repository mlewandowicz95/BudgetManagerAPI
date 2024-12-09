using BudgetManagerAPI.Data;
using BudgetManagerAPI.DTO;
using BudgetManagerAPI.Interfaces;
using BudgetManagerAPI.Models;
using BudgetManagerAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BudgetManagerAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
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
            if(!ModelState.IsValid)
            {
                _logger.LogError("Model state is not valid");
                return BadRequest(ModelState);
            }


            if (userDto.ConfirmPassword != userDto.Password)
            {
                _logger.LogError("Passwords are not same");
                return BadRequest(new { Message = "Passwords are not same" });
            }

            if (await _context.Users.AnyAsync(u => u.Email == userDto.Email))
            {
                _logger.LogError("User with email {email} already exists.", userDto.Email);
                return BadRequest(new { Message = "User already exists." });
            }



            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(userDto.Password);
            var activationToken  = Guid.NewGuid().ToString();

            var user = new User
            {
                Email = userDto.Email,
                PasswordHash = hashedPassword,
                IsActive = false,
                ActivationToken = activationToken
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new UserResponseDto
            {
                Id = user.Id,
                Email = user.Email,
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserRequestDto userDto)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogError("Model state is not valid");
                return BadRequest(ModelState);
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userDto.Email);
            if(user == null || !BCrypt.Net.BCrypt.Verify(userDto.Password, user.PasswordHash))
            {
                _logger.LogError("Invalid email or password.");
                return Unauthorized(new ErrorResponseDto { Message = "Invalid email or password." });
            }

            if(!user.IsActive)
            {
                _logger.LogError("Account is no activated.");
                return Unauthorized(new ErrorResponseDto { Message = "Account is no activated." });
            }

            user.LastLogin = DateTime.UtcNow;

            var token =_tokenService.GenerateToken(user.Id, user.Email);

            return Ok(new LoginResponseDto
            {
                Token = token,
                User = new UserResponseDto
                {
                    Id = user.Id,
                    Email = user.Email,
   
                }
            });
        }
    }
}
