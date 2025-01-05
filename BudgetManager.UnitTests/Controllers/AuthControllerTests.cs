using BudgetManagerAPI.Configurations;
using BudgetManagerAPI.Controllers;
using BudgetManagerAPI.Data;
using BudgetManagerAPI.DTO;
using BudgetManagerAPI.Interfaces;
using BudgetManagerAPI.Models;
using BudgetManagerAPI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BudgetManager.UnitTests.Controllers
{
    public class AuthControllerTests : IDisposable
    {
        private readonly Mock<ILogger<AuthController>> _loggerMock;
        private readonly DbContextOptions<AppDbContext> _dbContextOptions;
        private readonly AppDbContext _dbContext;
        private readonly TokenService _tokenService;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly AuthController _controller;

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

            _controller = new AuthController(_dbContext, _tokenService, _loggerMock.Object, _mockEmailService.Object);

            SeedDatabase();
        }

        public void Dispose()
        {
            _dbContext.Dispose();
        }

        private void SeedDatabase()
        {
            _dbContext.Users.Add(new User
            {
                Id = 1,
                Email = "exitinguser@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
                IsActive = true,


            });
            _dbContext.SaveChanges();
        }


        #region POST /api/Auth/register
        [Fact]
        public async Task Register_Should_Return_Ok_When_User_Is_Created()
        {
            // Arrange
            var newUser = new UserRequestDto
            {
                Email = "newuser@example.com",
                Password = "Password123!",
                ConfirmPassword = "Password123!"
            };

            var mockUrlHelper = new Mock<IUrlHelper>();
            mockUrlHelper
                .Setup(u => u.Action(It.IsAny<UrlActionContext>()))
                .Returns("https://test-url.com/activate");

            _controller.Url = mockUrlHelper.Object;
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };


            // act
            var result = await _controller.Register(newUser);

            // Assert
            Assert.IsType<OkObjectResult>(result);

            var okResult = result as OkObjectResult;
            Assert.NotNull(okResult);

            var response = okResult.Value as UserResponseDto;
            Assert.NotNull(response);
            Assert.Equal(newUser.Email, response.Email);

            var userInDb = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == newUser.Email);
            Assert.NotNull(userInDb);
            Assert.False(userInDb.IsActive);
            Assert.NotEmpty(userInDb.ActivationToken);

            _mockEmailService.Verify(es =>
                es.SendEmailAsync(
                    It.Is<string>(email => email == newUser.Email),
                    It.Is<string>(subject => subject.Contains("Activate your account")),
                    It.Is<string>(body => body.Contains("Click here to activate your account"))
                    ), Times.Once);
        }


        [Theory]
        [InlineData("", "Email is required")] // Pusty email
        [InlineData("invalid-email", "The Email field is not a valid e-mail address.")] // Nieprawidłowy format emaila
        public async Task Register_Should_Return_BadRequest_When_Email_Is_Invalid(string email, string expectedErrorMessage)
        {
            // Arrange
            var newUser = new UserRequestDto
            {
                Email = email,
                Password = "Password123!",
                ConfirmPassword = "Password123!"
            };

            var mockUrlHelper = new Mock<IUrlHelper>();
            mockUrlHelper
                .Setup(u => u.Action(It.IsAny<UrlActionContext>()))
                .Returns("https://test-url.com/activate");

            _controller.Url = mockUrlHelper.Object;

            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Ręczna walidacja ModelState
            var validationContext = new ValidationContext(newUser);
            var validationResults = new List<ValidationResult>();
            Validator.TryValidateObject(newUser, validationContext, validationResults, true);

            foreach (var validationResult in validationResults)
            {
                foreach (var memberName in validationResult.MemberNames)
                {
                    _controller.ModelState.AddModelError(memberName, validationResult.ErrorMessage);
                }
            }

            // Act
            var result = await _controller.Register(newUser);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
            var badRequestResult = result as BadRequestObjectResult;
            Assert.NotNull(badRequestResult);

            var errorResponse = badRequestResult.Value as ErrorResponseDto;
            Assert.NotNull(errorResponse);

            Assert.True(errorResponse.Errors.ContainsKey("Email"), "Brak błędu dla pola 'Email'.");
            var errors = errorResponse.Errors["Email"];
            Assert.Contains(expectedErrorMessage, errors);
        }

        [Theory]
        [InlineData("", "Password is required")] // Puste hasło
        [InlineData("password", "Password must have minimum eight characters, at least one uppercase letter, one lowercase letter, one number, and one special character.")] // Nie spełnia wymagań
        public async Task Register_Should_Return_BadRequest_When_Password_Is_Invalid(string password, string expectedErrorMessage)
        {
            // Arrange
            var newUser = new UserRequestDto
            {
                Email = "validemail@example.com",
                Password = password,
                ConfirmPassword = password
            };

            var mockUrlHelper = new Mock<IUrlHelper>();
            mockUrlHelper
                .Setup(u => u.Action(It.IsAny<UrlActionContext>()))
                .Returns("https://test-url.com/activate");

            _controller.Url = mockUrlHelper.Object;

            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Ręczna walidacja ModelState
            var validationContext = new ValidationContext(newUser);
            var validationResults = new List<ValidationResult>();
            Validator.TryValidateObject(newUser, validationContext, validationResults, true);

            foreach (var validationResult in validationResults)
            {
                foreach (var memberName in validationResult.MemberNames)
                {
                    _controller.ModelState.AddModelError(memberName, validationResult.ErrorMessage);
                }
            }

            // Act
            var result = await _controller.Register(newUser);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
            var badRequestResult = result as BadRequestObjectResult;
            Assert.NotNull(badRequestResult);

            var errorResponse = badRequestResult.Value as ErrorResponseDto;
            Assert.NotNull(errorResponse);

            // Weryfikacja komunikatu o błędzie
            Assert.Equal("Validation failed.", errorResponse.Message);
            Assert.Equal("VALIDATION_ERROR", errorResponse.ErrorCode);


            Assert.True(errorResponse.Errors.ContainsKey("Password"), "Brak błędu dla pola 'Password'.");
            var errors = errorResponse.Errors["Password"];
            Assert.Contains(expectedErrorMessage, errors);
        }


        [Theory]
        [InlineData("", "ConfirmPassword is required")] // Puste pole
        [InlineData("MismatchedPassword", "Passwords do not match.")]
        public async Task Register_Should_Return_BadRequest_When_ConfirmPassword_Is_Invalid(string confirmPassword, string expectedErrorMessage)
        {
            // Arrange
            var newUser = new UserRequestDto
            {
                Email = "validemail@example.com",
                Password = "Password123!",
                ConfirmPassword = confirmPassword
            };

            var mockUrlHelper = new Mock<IUrlHelper>();
            mockUrlHelper
                .Setup(u => u.Action(It.IsAny<UrlActionContext>()))
                .Returns("https://test-url.com/activate");

            _controller.Url = mockUrlHelper.Object;

            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Ręczna walidacja ModelState
            var validationContext = new ValidationContext(newUser);
            var validationResults = new List<ValidationResult>();
            Validator.TryValidateObject(newUser, validationContext, validationResults, true);

            foreach (var validationResult in validationResults)
            {
                foreach (var memberName in validationResult.MemberNames)
                {
                    _controller.ModelState.AddModelError(memberName, validationResult.ErrorMessage);
                }
            }

            // Act
            var result = await _controller.Register(newUser);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
            var badRequestResult = result as BadRequestObjectResult;
            Assert.NotNull(badRequestResult);

            var errorResponse = badRequestResult.Value as ErrorResponseDto;
            Assert.NotNull(errorResponse);
            Assert.Equal("Validation failed.", errorResponse.Message);
            Assert.True(errorResponse.Errors.ContainsKey("ConfirmPassword"), "Brak błędu dla pola 'ConfirmPassword'.");
            var errors = errorResponse.Errors["ConfirmPassword"];
            Assert.Contains(expectedErrorMessage, errors);
        }

        [Fact]
        public async Task Register_Should_Return_BadRequest_When_User_Already_Exists()
        {
            var users = await _dbContext.Users.ToListAsync();
            Console.WriteLine($"Users in DB: {users.Count}");
            foreach (var user in users)
            {
                Console.WriteLine($"User: {user.Email}");
            }

            // Arrange
            var newUser = new UserRequestDto
            {
                Email = "exitinguser@example.com",
                Password = "Password123!",
                ConfirmPassword = "Password123!",
            };

            var mockUrlHelper = new Mock<IUrlHelper>();
            mockUrlHelper
                .Setup(u => u.Action(It.IsAny<UrlActionContext>()))
                .Returns("https://test-url.com/activate");

            _controller.Url = mockUrlHelper.Object;

            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Upewnij się, że użytkownik istnieje w bazie
            var existingUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == "exitinguser@example.com");
            Assert.NotNull(existingUser);

            // Act
            var result = await _controller.Register(newUser);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
            var badRequestResult = result as BadRequestObjectResult;
            Assert.NotNull(badRequestResult);

            var errorResponse = badRequestResult.Value as ErrorResponseDto;
            Assert.NotNull(errorResponse);
            Assert.Contains("User already exists.", errorResponse.Message);
        }


        [Fact]
        public async Task Register_Should_Return_InternalServerError_When_Email_Sending_Fails()
        {
            // Arrange
            var newUser = new UserRequestDto
            {
                Email = "newuser@example.com",
                Password = "Password123!",
                ConfirmPassword = "Password123!"
            };

            _mockEmailService
    .Setup(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
    .ThrowsAsync(new Exception("Email sending failed."));

            var mockUrlHelper = new Mock<IUrlHelper>();
            mockUrlHelper
                .Setup(u => u.Action(It.IsAny<UrlActionContext>()))
                .Returns("https://test-url.com/activate");

            _controller.Url = mockUrlHelper.Object;
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Act
            var result = await _controller.Register(newUser);

            // Assert
            Assert.IsType<ObjectResult>(result);
            var objectResult = result as ObjectResult;
            Assert.NotNull(objectResult);
            Assert.Equal(500, objectResult.StatusCode);


            var errorResponse = objectResult.Value as ErrorResponseDto;
            Assert.NotNull(errorResponse);
            Assert.Equal("An error occurred while processing your request. Please try again later.", errorResponse.Message);
            Assert.Equal("INTERNAL_SERVER_ERROR", errorResponse.ErrorCode);

        }

        [Fact]
        public async Task Register_Should_Return_InternalServerError_When_Unexpected_Error_Occurred()
        {
            // Arrange
            var newUser = new UserRequestDto
            {
                Email = "newuser@example.com",
                Password = "Password123!",
                ConfirmPassword = "Password123!"
            };



            _dbContext.Database.EnsureDeleted(); 



            var mockUrlHelper = new Mock<IUrlHelper>();
            mockUrlHelper
                .Setup(u => u.Action(It.IsAny<UrlActionContext>()))
                .Returns("https://test-url.com/activate");

            _controller.Url = mockUrlHelper.Object;

            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Act
            var result = await _controller.Register(newUser);

            // Assert
            Assert.IsType<ObjectResult>(result); 
            var objectResult = result as ObjectResult;
            Assert.NotNull(objectResult);
            Assert.Equal(500, objectResult.StatusCode); 

            var errorResponse = objectResult.Value as ErrorResponseDto;
            Assert.NotNull(errorResponse);
            Assert.Equal("An error occurred while processing your request. Please try again later.", errorResponse.Message);
            Assert.Equal("INTERNAL_SERVER_ERROR", errorResponse.ErrorCode);

 
            _loggerMock.Verify(
     log => log.Log(
         LogLevel.Error,                
         It.IsAny<EventId>(),            
         It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("An error occurred while registering the user")), 
         It.IsAny<Exception>(),         
         It.IsAny<Func<It.IsAnyType, Exception, string>>() 
     ),
     Times.Once
 );
        }
        #endregion

    }
}
