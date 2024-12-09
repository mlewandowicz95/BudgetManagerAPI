using BudgetManagerAPI.Configurations;
using BudgetManagerAPI.Controllers;
using BudgetManagerAPI.Data;
using BudgetManagerAPI.DTO;
using BudgetManagerAPI.Interfaces;
using BudgetManagerAPI.Models;
using BudgetManagerAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BudgetManager.UnitTests.Controllers
{
    public class AuthControllerTests
    {
        private readonly Mock<ILogger<AuthController>> _loggerMock;
        private readonly DbContextOptions<AppDbContext> _dbContextOptions;
        private readonly AppDbContext _dbContext;
        private readonly TokenService _tokenService;
        private readonly Mock<IEmailService> _mockEmailService;


        public AuthControllerTests()
        {
            _loggerMock = new Mock<ILogger<AuthController>>();

            _dbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _dbContext = new AppDbContext(_dbContextOptions);

            // mock service with token
            _tokenService = new TokenService(new JwtSettings
            {
                SecretKey = "ThisIsASuperLongSecretKeyThatHasEnoughLength123!",
                Issuer = "TestIssuer",
                Audience = "TestAudience",
                ExpiryInMinutes = 60
            });

            _mockEmailService = new Mock<IEmailService>();
            _mockEmailService
.Setup(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
.Returns(Task.CompletedTask);
 


            SeedDatabase();
        }

        private void SeedDatabase()
        {
            _dbContext.Users.Add(new User
            {
                Email = "exitinguser@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
               
            });
            _dbContext.SaveChanges();
        }

        [Fact]
        public async Task Register_ShouldSaveUserAndSendEmail_WhenDataIsValid()
        {

            var controller = new AuthController(_dbContext, _tokenService, _loggerMock.Object, _mockEmailService.Object);
            var userDto = new UserRequestDto
            {
                Email = "test@example.com",
                Password = "SecurePassword123!",
                ConfirmPassword = "SecurePassword123!",
                
            };

            // ACT
            var result = await controller.Register(userDto);

            // Assert
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == "test@example.com");
            Assert.NotNull(user);
            Assert.False(user.IsActive);
            Assert.NotNull(user.ActivationToken);

            _mockEmailService.Verify(es =>
            es.SendEmailAsync(
                "test@example.com",
                It.Is<string>(s => s == "Activate your account"),
                It.Is<string>(body => body.Contains(user.ActivationToken))),
                Times.Once);

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal("Registration successful. Please check your email to activate your account.", ((dynamic)okResult.Value).Message);



        }



        [Fact]
        public async Task Register_ShouldReturnBadRequest_WhenEmailAlreadyExists()
        {
            var controller = new AuthController(_dbContext, _tokenService, _loggerMock.Object, _mockEmailService.Object);
            var duplicateUser = new UserRequestDto
            {
                Email = "exitinguser@example.com",
                Password = "password123",
                ConfirmPassword = "password123"
            };

            var result = await controller.Register(duplicateUser);

            var badRequestObjectResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestObjectResult.StatusCode);

            var userCount = _dbContext.Users.Count();
            Assert.Equal(1, userCount);
        }

        [Fact]
        public async Task Register_ShouldReturnBadRequest_WhenPasswordIsInvalid()
        {
            // Arrange
            var controller = new AuthController(_dbContext, _tokenService, _loggerMock.Object, _mockEmailService.Object);
            controller.ModelState.AddModelError("Password", "Password must have minimum six characters, at least one letter and one number.");

            var invalidUser = new UserRequestDto
            {
                Email = "test@example.com",
                Password = "abc123", // Niepoprawne hasło
                ConfirmPassword = "abc123"

            };

            // Act
            var result = await controller.Register(invalidUser);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequestResult.Value);
        }

        [Fact]
        public async Task Login_ShouldReturnToken_WhenCredentialsAreValid()
        {
            var controller = new AuthController(_dbContext, _tokenService, _loggerMock.Object, _mockEmailService.Object);

            var exitingUser = new UserRequestDto
            {
                Email = "exitinguser@example.com",
                Password = "password123",
                ConfirmPassword = "password123"
            };

            var result = await controller.Login(exitingUser);

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            var response = Assert.IsType<LoginResponseDto>(okResult.Value);
            Assert.NotNull(response.Token);
            Assert.IsType<string>(response.Token);
        }

        [Fact]
        public async Task Login_ShouldReturnUnathorized_WhenCredentialsAreInvalid()
        {
            var controller = new AuthController(_dbContext, _tokenService, _loggerMock.Object, _mockEmailService.Object);
            var invalidUser = new UserRequestDto
            {
                Email = "exitinguser@example.com",
                Password = "wrongPassword",
                ConfirmPassword = "wrongPassword"
            };

            var result = await controller.Login(invalidUser);

            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Equal(401, unauthorizedResult.StatusCode);

            var response = Assert.IsAssignableFrom<ErrorResponseDto>(unauthorizedResult.Value);
            Assert.Equal("Invalid email or password.", response.Message);
        }

        [Fact]
        public async Task Login_ShouldSetLastLogin_WhenCredentialsAreValid()
        {
            // Arrange
            var user = new User
            {
                Email = "test@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("ValidPassword"),
                ActivationToken = "12387128hrjrfdf12",
                IsActive = true
            };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var loginDto = new UserRequestDto
            {
                Email = "test@example.com",
                Password = "ValidPassword"
            };

            var controller = new AuthController(_dbContext, _tokenService, _loggerMock.Object, _mockEmailService.Object);


            // Act
            var result = await controller.Login(loginDto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var updatedUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == "test@example.com");
            Assert.NotNull(updatedUser.LastLogin);
            Assert.True((DateTime.UtcNow - updatedUser.LastLogin.Value).TotalSeconds < 5); // Sprawdź, czy data jest aktualna
        }

    }
}
