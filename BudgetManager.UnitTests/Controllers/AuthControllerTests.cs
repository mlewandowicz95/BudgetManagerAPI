using Azure;
using BudgetManagerAPI.Configurations;
using BudgetManagerAPI.Constants;
using BudgetManagerAPI.Controllers;
using BudgetManagerAPI.Data;
using BudgetManagerAPI.DTO;
using BudgetManagerAPI.Interfaces;
using BudgetManagerAPI.Models;
using BudgetManagerAPI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualStudio.TestPlatform.Common;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Linq.Dynamic.Core.Tokenizer;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
            var users = new List<User>
    {
        new User
        {
            Id = 1,
            Email = "exitinguser@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            IsActive = true,
        },
        new User
        {
            Id = 2,
            Email = "noactive@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            IsActive = false,
        }
    };

            _dbContext.Users.AddRange(users);
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

            // Act
            var result = await _controller.Register(newUser);

            // Assert
            Assert.IsType<OkObjectResult>(result);

            var okResult = result as OkObjectResult;
            Assert.NotNull(okResult);

            var response = okResult.Value as SuccessResponseDto<UserResponseDto>;
            Assert.NotNull(response);
            Assert.True(response.Success, "Response should indicate success.");
            Assert.Equal("User has been registered.", response.Message);
            Assert.Equal(newUser.Email, response.Data.Email); // Pobieranie Email z Data
            Assert.NotNull(response.TraceId);

            // Sprawdzenie, czy użytkownik został zapisany w bazie
            var userInDb = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == newUser.Email);
            Assert.NotNull(userInDb);
            Assert.False(userInDb.IsActive);
            Assert.NotEmpty(userInDb.ActivationToken);

            // Weryfikacja, czy email aktywacyjny został wysłany
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
            Assert.False(errorResponse.Success, "Response should not indicate success.");
            // Weryfikacja komunikatu o błędzie
            Assert.Equal("Validation failed.", errorResponse.Message);
            Assert.Equal(ErrorCodes.ValidationError, errorResponse.ErrorCode);


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
            Assert.False(errorResponse.Success, "Response should not indicate success.");

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
            Assert.False(errorResponse.Success, "Response should not indicate success.");
            Assert.Equal(ErrorCodes.UserAlreadyExists, errorResponse.ErrorCode);
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
            Assert.False(errorResponse.Success, "Response should not indicate success.");

            Assert.Equal("An error occurred while processing your request. Please try again later.", errorResponse.Message);
            Assert.Equal(ErrorCodes.InternalServerError, errorResponse.ErrorCode);

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
            Assert.False(errorResponse.Success, "Response should not indicate success.");

            Assert.Equal("An error occurred while processing your request. Please try again later.", errorResponse.Message);
            Assert.Equal(ErrorCodes.InternalServerError, errorResponse.ErrorCode);


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

        #region /api/Auth/confirm-email
        [Fact]
        public async Task ConfirmEmail_Should_Return_BadRequest_When_Token_Is_Missing()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // act
            var result = await _controller.ConfirmEmail(null);

            // assert 
            Assert.IsType<BadRequestObjectResult>(result);
            var badRequestResult = result as BadRequestObjectResult;
            Assert.NotNull(badRequestResult);

            var errorResponse = badRequestResult.Value as ErrorResponseDto;
            Assert.NotNull(errorResponse);
            Assert.False(errorResponse.Success, "Response should not indicate success.");

            Assert.Equal(ErrorCodes.MissingToken, errorResponse.ErrorCode);
            Assert.Equal("Invalid confirmation request.", errorResponse.Message);
        }

        [Fact]
        public async Task ConfirmEmail_Should_Return_NotFound_When_Token_Is_Invalid()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };


            // Act
            var result = await _controller.ConfirmEmail("invalid-token");

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
            var notFoundResult = result as NotFoundObjectResult;
            Assert.NotNull(notFoundResult);

            var errorResponse = notFoundResult.Value as ErrorResponseDto;
            Assert.NotNull(errorResponse);
            Assert.False(errorResponse.Success, "Response should not indicate success.");

            Assert.Equal(ErrorCodes.InvalidToken, errorResponse.ErrorCode);
            Assert.Equal("Invalid activation token.", errorResponse.Message);
        }

        [Fact]
        public async Task ConfirmEmail_Should_Return_BadRequest_When_User_Is_Already_Active()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
            // Arrange
            var activeUser = new User
            {
                Email = "active@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!@"),
                ActivationToken = "active-token",
                IsActive = true
            };
            _dbContext.Users.Add(activeUser);
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _controller.ConfirmEmail("active-token");

            // assert 
            Assert.IsType<BadRequestObjectResult>(result);
            var badRequestResult = result as BadRequestObjectResult;
            Assert.NotNull(badRequestResult);

            var errorResponse = badRequestResult.Value as ErrorResponseDto;
            Assert.NotNull(errorResponse);
            Assert.False(errorResponse.Success, "Response should not indicate success.");

            Assert.Equal(ErrorCodes.UserAlreadyActive, errorResponse.ErrorCode);
            Assert.Equal("User is already active.", errorResponse.Message);
        }

        [Fact]
        public async Task ConfirmEmail_Should_Return_Ok_When_Token_Is_Valid()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            var activeUser = new User
            {
                Email = "active@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!@"),
                ActivationToken = "good-token",
                IsActive = false,
            };
            _dbContext.Users.Add(activeUser);
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _controller.ConfirmEmail("good-token");

            // Assert
            Assert.IsType<OkObjectResult>(result);

            var okResult = result as OkObjectResult;
            Assert.NotNull(okResult);

            var response = okResult.Value as SuccessResponseDto<ConfirmEmailResponseDto>;
            Assert.NotNull(response);

            // Sprawdzanie wartości SuccessResponseDto
            Assert.True(response.Success, "Response should indicate success.");
            Assert.Equal("Account activated successfully. You can now log in.", response.Message);
            Assert.Equal("active@example.com", response.Data.Email);

            // Sprawdzenie, czy użytkownik w bazie został aktywowany
            var userInDb = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == "active@example.com");
            Assert.NotNull(userInDb);
            Assert.True(userInDb.IsActive, "User should be marked as active.");
            Assert.Empty(userInDb.ActivationToken); // Token powinien być usunięty


            _loggerMock.Verify(
                log => log.Log<It.IsAnyType>(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) =>
                        v.ToString().Contains("User has been activated.") &&
                        v.ToString().Contains($"Email: active@example.com") &&
                        v.ToString().Contains($"TraceId: {httpContext.TraceIdentifier}")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()
                ),
                Times.Once
            );

        }

        [Fact]
        public async Task ConfirmEmail_Should_Return_InternalServerError_When_Exception_Is_Thrown()
        {
            // Arrange
            var token = "valid-token";

            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Dodanie użytkownika z tokenem do bazy
            var user = new User
            {
                Email = "test@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
                ActivationToken = token,
                IsActive = false
            };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            // Symulacja wyjątku - za pomocą nadpisania SaveChangesAsync
            var mockDbContext = new Mock<AppDbContext>(_dbContextOptions);
            mockDbContext
                .Setup(db => db.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Simulated database error"));

            var controllerWithMockedDb = new AuthController(mockDbContext.Object, _tokenService, _loggerMock.Object, _mockEmailService.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = httpContext
                }
            };

            // Act
            var result = await controllerWithMockedDb.ConfirmEmail(token);

            // Assert
            Assert.IsType<ObjectResult>(result);
            var objectResult = result as ObjectResult;
            Assert.NotNull(objectResult);
            Assert.Equal(500, objectResult.StatusCode);

            var errorResponse = objectResult.Value as ErrorResponseDto;
            Assert.NotNull(errorResponse);
            Assert.False(errorResponse.Success, "Response should indicate failure.");
            Assert.Equal(ErrorCodes.InternalServerError, errorResponse.ErrorCode);
            Assert.Equal("An error occurred while processing your request. Please try again later.", errorResponse.Message);

        }



        #endregion


        #region /api/Auth/resend-activasion-link
        [Fact]
        public async Task ResendActivationLink_Should_Return_Ok_When_Link_Is_Resent()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            var dto = new ResendActivationLinkRequestDto
            {
                Email = "noactive@example.com"
            };

            var mockUrlHelper = new Mock<IUrlHelper>();
            mockUrlHelper
                .Setup(u => u.Action(It.IsAny<UrlActionContext>()))
                .Returns("https://test-url.com/activate");

            _controller.Url = mockUrlHelper.Object;

            // act
            var result = await _controller.ResendActivationLink(dto);

            // Assert
            Assert.IsType<OkObjectResult>(result);

            var okResult = result as OkObjectResult;
            Assert.NotNull(okResult);

            var response = okResult.Value as SuccessResponseDto<ResendActivationLinkResponseDto>;
            Assert.NotNull(response);
            Assert.True(response.Success, "Respone should indicate success.");
            Assert.Equal("Activation link has been resent.", response.Message);
            Assert.NotNull(response.TraceId);

            var data = response.Data;
            Assert.NotNull(data);
            Assert.Equal(dto.Email, response.Data.Email);

            var userInDb = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            Assert.NotNull(userInDb);
            Assert.False(userInDb.IsActive, "User should still be inactive.");
            Assert.NotEmpty(userInDb.ActivationToken);

            _mockEmailService.Verify(es =>
            es.SendEmailAsync(
                It.Is<string>(email => email == dto.Email),
                It.Is<string>(subject => subject.Contains("Activate your account")),
                It.Is<string>(body => body.Contains("Click the link to activate your account"))
                ), Times.Once);
        }

        [Theory]
        [InlineData("", "Email is required")] // Pusty email
        public async Task ResendActivationLink_ShouldReturn_BadRequest_When_Email_Is_Missing(string email, string expectedErrorMessage)
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            var dto = new ResendActivationLinkRequestDto
            {
                Email = email,
            };


            var mockUrlHelper = new Mock<IUrlHelper>();
            mockUrlHelper
                .Setup(u => u.Action(It.IsAny<UrlActionContext>()))
                .Returns("https://test-url.com/activate");

            _controller.Url = mockUrlHelper.Object;



            // Ręczna walidacja ModelState
            var validationContext = new ValidationContext(dto);
            var validationResults = new List<ValidationResult>();
            Validator.TryValidateObject(dto, validationContext, validationResults, true);

            foreach (var validationResult in validationResults)
            {
                foreach (var memberName in validationResult.MemberNames)
                {
                    _controller.ModelState.AddModelError(memberName, validationResult.ErrorMessage);
                }
            }


            // Act
            var result = await _controller.ResendActivationLink(dto);

            // Arrange
            Assert.IsType<BadRequestObjectResult>(result);

            var badRequestResult = result as BadRequestObjectResult;
            Assert.NotNull(badRequestResult);

            var response = badRequestResult.Value as ErrorResponseDto;
            Assert.NotNull(response);
            Assert.False(response.Success, "Response should indicate failure.");
            Assert.Equal("Validation failed.", response.Message);
            Assert.Equal(ErrorCodes.ValidationError, response.ErrorCode);
            Assert.NotNull(response.TraceId);

            // Sprawdzenie błędów w odpowiedzi
            Assert.NotNull(response.Errors);
            Assert.True(response.Errors.ContainsKey("Email"), "Response should contain validation errors for Email.");
            Assert.Contains(expectedErrorMessage, response.Errors["Email"]);
        }

        [Fact]
        public async Task ResendActivationLink_ShouldReturn_NotFound_When_User_Does_Not_Exist()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            var dto = new ResendActivationLinkRequestDto
            {
                Email = "noexistusers@example.com"
            };

            var mockUrlHelper = new Mock<IUrlHelper>();
            mockUrlHelper
                .Setup(u => u.Action(It.IsAny<UrlActionContext>()))
                .Returns("https://test-url.com/activate");

            _controller.Url = mockUrlHelper.Object;

            // act
            var result = await _controller.ResendActivationLink(dto);

            Assert.IsType<NotFoundObjectResult>(result);

            var notFoundObjectResult = result as NotFoundObjectResult;
            Assert.NotNull(notFoundObjectResult);

            var response = notFoundObjectResult.Value as ErrorResponseDto;
            Assert.NotNull(response);
            Assert.False(response.Success, "Response should indicate failure.");
            Assert.Equal("User not found.", response.Message);
            Assert.Equal(ErrorCodes.UserNotFound, response.ErrorCode);
            Assert.NotNull(response.TraceId);


        }

        [Fact]
        public async Task ResendActivationLink_ShouldReturn_BadRequest_When_User_Is_Already_Active()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            var dto = new ResendActivationLinkRequestDto
            {
                Email = "exitinguser@example.com",
            };

            var mockUrlHelper = new Mock<IUrlHelper>();
            mockUrlHelper
                .Setup(u => u.Action(It.IsAny<UrlActionContext>()))
                .Returns("https://test-url.com/activate");

            _controller.Url = mockUrlHelper.Object;

            // act
            var result = await _controller.ResendActivationLink(dto);

            Assert.IsType<BadRequestObjectResult>(result);

            var badRequestResult = result as BadRequestObjectResult;
            Assert.NotNull(badRequestResult);

            var response = badRequestResult.Value as ErrorResponseDto;
            Assert.NotNull(response);
            Assert.False(response.Success, "Response should indicate failure.");
            Assert.Equal("User is already active.", response.Message);
            Assert.Equal(ErrorCodes.UserAlreadyActive, response.ErrorCode);
            Assert.NotNull(response.TraceId);

        }

        [Fact]
        public async Task ResendActivationLink_Should_Return_InternalServerError_When_Exception_Is_Thrown()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            var dto = new ResendActivationLinkRequestDto
            {
                Email = "noactive@example.com"
            };


      
            _dbContext.Dispose(); // Wymuszenie błędu przy kolejnym dostępie do bazy

            var mockUrlHelper = new Mock<IUrlHelper>();
            mockUrlHelper
                .Setup(u => u.Action(It.IsAny<UrlActionContext>()))
                .Returns("https://test-url.com/activate");

            _controller.Url = mockUrlHelper.Object;

            // Act
            var result = await _controller.ResendActivationLink(dto);

            // Assert
            Assert.IsType<ObjectResult>(result);
            var objectResult = result as ObjectResult;
            Assert.NotNull(objectResult);
            Assert.Equal(500, objectResult.StatusCode);

            var errorResponse = objectResult.Value as ErrorResponseDto;
            Assert.NotNull(errorResponse);
            Assert.False(errorResponse.Success, "Response should not indicate success.");
            Assert.Equal("An error occurred while processing your request. Please try again later.", errorResponse.Message);
            Assert.Equal(ErrorCodes.InternalServerError, errorResponse.ErrorCode);

            _loggerMock.Verify(
                log => log.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("An unexpected error occurred.")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()
                ),
                Times.Once
            );
        }

        #endregion

        #region /api/Auth/login

        [Fact]
        public async Task Login_Should_Return_Ok_When_Credentials_Are_Valid()
        {

            // Arrange
            var validUser = new User
            {
                Email = "valid@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("ValidPassword123!"),
                IsActive = true,
                Role = "User"
            };
            _dbContext.Users.Add(validUser);
            await _dbContext.SaveChangesAsync();

            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };


            var dto = new LoginRequestDto
            {
                Email = "valid@example.com",
                Password = "ValidPassword123!"
            };

            // Act
            var result = await _controller.Login(dto);

            Assert.IsType<OkObjectResult>(result);
            var objectResult = result as OkObjectResult;
            Assert.NotNull(objectResult);

            var response = objectResult.Value as SuccessResponseDto<LoginResponseDto>;
            Assert.NotNull(response);
            Assert.True(response.Success, "Respone should indicate success.");
            Assert.Equal("Login successful.", response.Message);
            Assert.NotNull(response.TraceId);


            var userInDb = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            Assert.NotNull(userInDb);
            Assert.True(userInDb.IsActive, "User should be active.");
            Assert.NotNull(response.Data.Token);
        }

        [Theory]
        [InlineData("", "Password123!", "Email is required.")]
        [InlineData("exitinguser@example.com", "", "Password is required.")]
        public async Task Login_Should_Return_BadRequest_When_Model_Is_Invalid(string email, string password, string expectedErrorMessage)
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            var loginRequest = new LoginRequestDto
            {
                Email = email,
                Password = password,
            };

            // Ręczna walidacja ModelState
            var validationContext = new ValidationContext(loginRequest);
            var validationResults = new List<ValidationResult>();
            Validator.TryValidateObject(loginRequest, validationContext, validationResults, true);

            foreach (var validationResult in validationResults)
            {
                foreach (var memberName in validationResult.MemberNames)
                {
                    _controller.ModelState.AddModelError(memberName, validationResult.ErrorMessage);
                }
            }


            // Act
            var result = await _controller.Login(loginRequest);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);

            var badRequestObjectResult = result as BadRequestObjectResult;
            Assert.NotNull(badRequestObjectResult);

            var response = badRequestObjectResult.Value as ErrorResponseDto;
            Assert.NotNull(response);
            Assert.False(response.Success, "Response should not indicate success.");
            Assert.Equal("Validation failed.", response.Message);
            Assert.Equal(ErrorCodes.ValidationError, response.ErrorCode);
            Assert.NotNull(response.TraceId);

            Assert.True(response.Errors.ContainsKey("Email") || response.Errors.ContainsKey("Password"));
            Assert.Contains(expectedErrorMessage, response.Errors.Values.SelectMany(e => e));

        }

        [Fact]
        public async Task Login_Should_Return_Unauthorized_When_Email_Or_Password_Is_Invalid()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            var loginRequest = new LoginRequestDto
            {
                Email = "wrong@example.com",
                Password = "WrongPassword123!"
            };

            // Act
            var result = await _controller.Login(loginRequest);

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);

            var unauthorizedResult = result as UnauthorizedObjectResult;
            Assert.NotNull(unauthorizedResult);

            var response = unauthorizedResult.Value as ErrorResponseDto;
            Assert.NotNull(response);
            Assert.False(response.Success, "Response should not indicate success.");
            Assert.Equal("Invalid email or password.", response.Message);
            Assert.Equal(ErrorCodes.InvalidCredentials, response.ErrorCode);
            Assert.NotNull(response.TraceId);

        }

        [Fact]
        public async Task Login_Should_Return_Unauthorized_When_User_Is_Not_Active()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            var loginRequest = new LoginRequestDto
            {
                Email = "noactive@example.com",
                Password = "Password123!"
            };

            // Act
            var result = await _controller.Login(loginRequest);


            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
            var unauthorizedResult = result as UnauthorizedObjectResult;
            Assert.NotNull(unauthorizedResult);

            var response = unauthorizedResult.Value as ErrorResponseDto;
            Assert.NotNull(response);
            Assert.False(response.Success, "Response should not indicate success.");
            Assert.Equal("Account is not activated.", response.Message);
            Assert.Equal(ErrorCodes.AccountNotActivated, response.ErrorCode);
            Assert.NotNull(response.TraceId);
        }


        [Fact]
        public async Task Login_Should_Return_BadRequest_When_Server_Throw_Exception()
        {
            // Arrange


            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            _dbContext.Dispose();

            var dto = new LoginRequestDto
            {
                Email = "exitinguser@example.com",
                Password = "Password123!"
            };

            // Act
            var result = await _controller.Login(dto);

            // Assert
            Assert.IsType<ObjectResult>(result);
            var objectResult = result as ObjectResult;
            Assert.NotNull(objectResult);
            Assert.Equal(500, objectResult.StatusCode);

            var errorResponse = objectResult.Value as ErrorResponseDto;
            Assert.NotNull(errorResponse);
            Assert.False(errorResponse.Success, "Response should not indicate success.");
            Assert.Equal("An error occurred while processing your request. Please try again later.", errorResponse.Message);
            Assert.Equal(ErrorCodes.InternalServerError, errorResponse.ErrorCode);

            _loggerMock.Verify(
                log => log.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("An unexpected error occurred while logging in.")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()
                ),
                Times.Once
            );
        }





        #endregion

        #region /api/Auth/request-password-reset

        [Theory]
        [InlineData("", "Email is required.")]
        [InlineData(null, "Email is required.")]

        public async Task RequestPasswordReset_Should_Return_BadRequest_When_Model_Is_Invalid(string email, string expectedErrorMessage)
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            var requestDto = new ResetPasswordRequestDto
            {
                Email = email
            };

            // Ręczna walidacja ModelState
            var validationContext = new ValidationContext(requestDto);
            var validationResults = new List<ValidationResult>();
            Validator.TryValidateObject(requestDto, validationContext, validationResults, true);

            foreach (var validationResult in validationResults)
            {
                foreach (var memberName in validationResult.MemberNames)
                {
                    _controller.ModelState.AddModelError(memberName, validationResult.ErrorMessage);
                }
            }


            // Act
            var result = await _controller.RequestPasswordReset(requestDto);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);

            var badRequestObjectResult = result as BadRequestObjectResult;
            Assert.NotNull(badRequestObjectResult);

            var response = badRequestObjectResult.Value as ErrorResponseDto;
            Assert.NotNull(response);
            Assert.False(response.Success, "Response should not indicate success.");
            Assert.Equal("Validation failed.", response.Message);
            Assert.Equal(ErrorCodes.ValidationError, response.ErrorCode);
            Assert.NotNull(response.TraceId);

            Assert.True(response.Errors.ContainsKey("Email"));
            Assert.Contains(expectedErrorMessage, response.Errors.Values.SelectMany(e => e));
        }

        [Fact]
        public async Task RequestPasswordReset_Should_Return_NotFound_When_User_Does_Not_Exist()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            var requestDto = new ResetPasswordRequestDto
            {
                Email = "notexitinguser@example.com"
            };

            // Act
            var result = await _controller.RequestPasswordReset(requestDto);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);

            var notFoundResult = result as NotFoundObjectResult;
            Assert.NotNull(notFoundResult);

            var response = notFoundResult.Value as ErrorResponseDto;
            Assert.NotNull(response);
            Assert.False(response.Success, "Response should not indicate success.");
            Assert.Equal("User with email not found.", response.Message);
            Assert.Equal(ErrorCodes.UserNotFound, response.ErrorCode);
            Assert.NotNull(response.TraceId);
        }

        [Fact]
        public async Task RequestPasswordReset_Should_Return_Ok_When_Token_Is_Generated()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            var requestDto = new ResetPasswordRequestDto
            {
                Email = "exitinguser@example.com",
            };

            var mockUrlHelper = new Mock<IUrlHelper>();
            mockUrlHelper
                .Setup(u => u.Action(It.IsAny<UrlActionContext>()))
                .Returns("https://test-url.com/reset-password");

            _controller.Url = mockUrlHelper.Object;

            // Act
            var result = await _controller.RequestPasswordReset(requestDto);

            // Assert
            Assert.IsType<OkObjectResult>(result);

            var objectResult = result as OkObjectResult;
            Assert.NotNull(objectResult);

            var response = objectResult.Value as SuccessResponseDto<ResetPasswordResponseDto>;
            Assert.NotNull(response);
            Assert.True(response.Success, "Response should indicate success.");
            Assert.Equal("Password reset link sent to you email.", response.Message);
            Assert.NotNull(response.TraceId);
            Assert.NotNull(response.Data);
            Assert.Equal("exitinguser@example.com", response.Data.Email);

            var userInDb = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == "exitinguser@example.com");
            Assert.NotNull(userInDb);
            Assert.NotNull(userInDb.ResetToken);
            Assert.True(userInDb.ResetTokenExpiry > DateTime.UtcNow);

            _mockEmailService.Verify(es =>
    es.SendEmailAsync(
        It.Is<string>(email => email == "exitinguser@example.com"),
        It.Is<string>(subject => subject.Contains("Reset your password")),
        It.Is<string>(body => body.Contains("Use the following link to reset your password"))
    ),
    Times.Once
);
        }

        [Fact]
        public async Task RequestPasswordReset_Should_Return_InternalServerError_When_Exception_Is_Thrown()
        {
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            _dbContext.Dispose();

            var dto = new ResetPasswordRequestDto
            {
                Email = "exitinguser@example.com",
            };

            // Act
            var result = await _controller.RequestPasswordReset(dto);

            // Assert
            Assert.IsType<ObjectResult>(result);
            var objectResult = result as ObjectResult;
            Assert.NotNull(objectResult);
            Assert.Equal(500, objectResult.StatusCode);

            var errorResponse = objectResult.Value as ErrorResponseDto;
            Assert.NotNull(errorResponse);
            Assert.False(errorResponse.Success, "Response should not indicate success.");
            Assert.Equal("An error occurred while processing your request. Please try again later.", errorResponse.Message);
            Assert.Equal(ErrorCodes.InternalServerError, errorResponse.ErrorCode);

            _loggerMock.Verify(
                log => log.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("An unexpected error occurred while request password reset.")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()
                ),
                Times.Once
            );
        }


        [Fact]
        public async Task RequestPasswordReset_Should_Include_Valid_Link_In_Email()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };


            var dto = new ResetPasswordRequestDto
            {
                Email = "exitinguser@example.com",
            };

            var mockUrlHelper = new Mock<IUrlHelper>();
            mockUrlHelper
                .Setup(u => u.Action(It.IsAny<UrlActionContext>()))
                .Returns("https://test-url.com/reset-password");

            _controller.Url = mockUrlHelper.Object;

            // Act
            var result = await _controller.RequestPasswordReset(dto);

            // Assert
            _mockEmailService.Verify(es =>
                es.SendEmailAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.Is<string>(body => body.Contains("https://test-url.com/reset-password"))
                ),
                Times.Once);
        }

        [Fact]
        public async Task RequestPasswordReset_Should_Update_User_With_Token_And_Expiry()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            var requestDto = new ResetPasswordRequestDto
            {
                Email = "exitinguser@example.com"
            };

            // Act
            await _controller.RequestPasswordReset(requestDto);

            // Assert
            var userInDb = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == requestDto.Email);
            Assert.NotNull(userInDb);
            Assert.NotEmpty(userInDb.ResetToken);
            Assert.True(userInDb.ResetTokenExpiry > DateTime.UtcNow);
        }

        [Fact]
        public async Task RequestPasswordReset_Should_Return_InternalServerError_When_EmailService_Fails()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            var requestDto = new ResetPasswordRequestDto
            {
                Email = "exitinguser@example.com"
            };

            _mockEmailService
                .Setup(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Email sending failed"));

            var mockUrlHelper = new Mock<IUrlHelper>();
            mockUrlHelper
                .Setup(u => u.Action(It.IsAny<UrlActionContext>()))
                .Returns("https://test-url.com/reset-password");

            _controller.Url = mockUrlHelper.Object;

            // Act
            var result = await _controller.RequestPasswordReset(requestDto);

            // Assert
            Assert.IsType<ObjectResult>(result);

            var objectResult = result as ObjectResult;
            Assert.NotNull(objectResult);
            Assert.Equal(500, objectResult.StatusCode);

            var errorResponse = objectResult.Value as ErrorResponseDto;
            Assert.NotNull(errorResponse);
            Assert.False(errorResponse.Success, "Response should indicate failure.");
            Assert.Equal("An error occurred while processing your request. Please try again later.", errorResponse.Message);
            Assert.Equal(ErrorCodes.InternalServerError, errorResponse.ErrorCode);
        }

        #endregion

        #region /api/Auth/reset-password

        [Theory]
        [InlineData("", "ValidPassword123!", "Token is required.")]
        [InlineData("valid-token", "", "New password is required.")]
        [InlineData("", "", "Token is required.")]
        public async Task ResetPassword_Should_Return_BadRequest_When_Model_Is_Invalid(string token, string newPassword, string expectedErrorMessage)
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            var resetPasswordDto = new ResetPasswordDto
            {
                Token = token,
                NewPassword = newPassword
            };

            // Act
            var result = await _controller.ResetPassword(resetPasswordDto);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);

            var badRequestResult = result as BadRequestObjectResult;
            Assert.NotNull(badRequestResult);

            var response = badRequestResult.Value as ErrorResponseDto;
            Assert.NotNull(response);
            Assert.False(response.Success, "Response should indicate failure.");
            Assert.Equal(ErrorCodes.ValidationError, response.ErrorCode);
            Assert.Equal("Validation failed.", response.Message);
            Assert.NotNull(response.TraceId);

            Assert.Contains(expectedErrorMessage, response.Errors.Values.SelectMany(e => e));
        }


        [Fact]
        public async Task ResetPassword_Should_Return_BadRequest_When_User_Not_Found()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            var resetPasswordDto = new ResetPasswordDto
            {
                Token = "non-existing-token",
                NewPassword = "ValidPassword123!"
            };

            // Act
            var result = await _controller.ResetPassword(resetPasswordDto);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);

            var badRequestResult = result as BadRequestObjectResult;
            Assert.NotNull(badRequestResult);

            var response = badRequestResult.Value as ErrorResponseDto;
            Assert.NotNull(response);
            Assert.False(response.Success, "Response should indicate failure.");
            Assert.Equal(ErrorCodes.UserNotFound, response.ErrorCode);
            Assert.Equal("User not found.", response.Message);
            Assert.NotNull(response.TraceId);
        }

        [Fact]
        public async Task ResetPassword_Should_Return_BadRequest_When_Token_Is_Missing()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            var resetPasswordDto = new ResetPasswordDto
            {
                Token = string.Empty, // Brak tokenu
                NewPassword = "ValidPassword123!"
            };

            // Act
            var result = await _controller.ResetPassword(resetPasswordDto);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);

            var badRequestResult = result as BadRequestObjectResult;
            Assert.NotNull(badRequestResult);

            var response = badRequestResult.Value as ErrorResponseDto;
            Assert.NotNull(response);
            Assert.False(response.Success, "Response should indicate failure.");
            Assert.Equal(ErrorCodes.ValidationError, response.ErrorCode);
            Assert.Equal("Validation failed.", response.Message);
            Assert.NotNull(response.TraceId);

            // Sprawdzamy, że w odpowiedzi znajdują się odpowiednie błędy walidacji
            Assert.NotNull(response.Errors);
            Assert.Contains(nameof(resetPasswordDto.Token), response.Errors.Keys);
            Assert.Contains("Token is required.", response.Errors[nameof(resetPasswordDto.Token)]);
        }

        [Fact]
        public async Task ResetPassword_Should_Return_BadRequest_When_Token_Is_Expired()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            var expiredUser = new User
            {
                Email = "expired@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPassword123!"),
                ResetToken = "expired-token",
                ResetTokenExpiry = DateTime.UtcNow.AddHours(-1) // Token wygasł
            };

            _dbContext.Users.Add(expiredUser);
            await _dbContext.SaveChangesAsync();

            var resetPasswordDto = new ResetPasswordDto
            {
                Token = "expired-token",
                NewPassword = "NewValidPassword123!"
            };

            // Act
            var result = await _controller.ResetPassword(resetPasswordDto);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);

            var badRequestResult = result as BadRequestObjectResult;
            Assert.NotNull(badRequestResult);

            var response = badRequestResult.Value as ErrorResponseDto;
            Assert.NotNull(response);
            Assert.False(response.Success, "Response should indicate failure.");
            Assert.Equal(ErrorCodes.ExpiredToken, response.ErrorCode);
            Assert.Equal("Expired token.", response.Message);
            Assert.NotNull(response.TraceId);
        }

        [Fact]
        public async Task ResetPassword_Should_Return_InternalServerError_When_Exception_Is_Thrown()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            var validUser = new User
            {
                Email = "user@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPassword123!"),
                ResetToken = "valid-token",
                ResetTokenExpiry = DateTime.UtcNow.AddHours(1)
            };

            _dbContext.Users.Add(validUser);
            await _dbContext.SaveChangesAsync();

            var resetPasswordDto = new ResetPasswordDto
            {
                Token = "valid-token",
                NewPassword = "NewPassword123!"
            };

            // Symulacja wyjątku poprzez usunięcie kontekstu
            _dbContext.Dispose();

            // Act
            var result = await _controller.ResetPassword(resetPasswordDto);

            // Assert
            Assert.IsType<ObjectResult>(result);

            var objectResult = result as ObjectResult;
            Assert.NotNull(objectResult);
            Assert.Equal(500, objectResult.StatusCode);

            var response = objectResult.Value as ErrorResponseDto;
            Assert.NotNull(response);
            Assert.False(response.Success, "Response should not indicate success.");
            Assert.Equal(ErrorCodes.InternalServerError, response.ErrorCode);
            Assert.Equal("An error occurred while processing your request. Please try again later.", response.Message);
            Assert.NotNull(response.TraceId);

            // Sprawdzenie logowania błędu
            _loggerMock.Verify(
                log => log.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("An unexpected error occurred while resetting the password.")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()
                ),
                Times.Once
            );
        }


        #endregion

        #region /api/Auth/logout
        [Fact]
        public async Task Logout_Should_Return_BadRequest_When_Authorization_Header_Is_Missing()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            // Act
            var result = await _controller.Logout();

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);

            var badRequestResult = result as BadRequestObjectResult;
            Assert.NotNull(badRequestResult);

            var response = badRequestResult.Value as ErrorResponseDto;
            Assert.NotNull(response);
            Assert.False(response.Success);
            Assert.Equal("Authorization header is missing or invalid.", response.Message);
            Assert.Equal(ErrorCodes.InvalidAuthorizationHeader, response.ErrorCode);
            Assert.NotNull(response.TraceId);
        }

        [Fact]
        public async Task Logout_Should_Return_BadRequest_When_Authorization_Header_Is_Invalid()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["Authorization"] = "InvalidTokenFormat";
            _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            // Act
            var result = await _controller.Logout();

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);

            var badRequestResult = result as BadRequestObjectResult;
            Assert.NotNull(badRequestResult);

            var response = badRequestResult.Value as ErrorResponseDto;
            Assert.NotNull(response);
            Assert.False(response.Success);
            Assert.Equal("Authorization header is missing or invalid.", response.Message);
            Assert.Equal(ErrorCodes.InvalidAuthorizationHeader, response.ErrorCode);
            Assert.NotNull(response.TraceId);
        }

        [Fact]
        public async Task Logout_Should_Return_BadRequest_When_Token_Is_Invalid()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["Authorization"] = "Bearer InvalidToken";
            _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            // Mockowanie GetTokenExpiryDate, aby zwróciło null
            _controller.GetType()
                .GetMethod("GetTokenExpiryDate", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_controller, new object[] { "InvalidToken" });

            // Act
            var result = await _controller.Logout();

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);

            var badRequestResult = result as BadRequestObjectResult;
            Assert.NotNull(badRequestResult);

            var response = badRequestResult.Value as ErrorResponseDto;
            Assert.NotNull(response);
            Assert.False(response.Success);
            Assert.Equal("Invalid token.", response.Message);
            Assert.Equal(ErrorCodes.InvalidToken, response.ErrorCode);
            Assert.NotNull(response.TraceId);
        }

        [Fact]
        public async Task Logout_Should_Return_BadRequest_When_Token_Has_Expired()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            // Wygeneruj token, który symuluje wygasły token
            var expiredToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOjE2MDAwMDAwMDB9._token_signature";
            httpContext.Request.Headers["Authorization"] = $"Bearer {expiredToken}";

            // Użyj refleksji do wywołania prywatnej metody
            var method = typeof(AuthController).GetMethod("GetTokenExpiryDate", BindingFlags.NonPublic | BindingFlags.Instance);
            var expiryDate = method.Invoke(_controller, new object[] { expiredToken }) as DateTime?;

            // Upewnij się, że data wygaśnięcia jest w przeszłości
            Assert.NotNull(expiryDate);
            Assert.True(expiryDate < DateTime.UtcNow);

            // Act
            var result = await _controller.Logout();

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);

            var badRequestResult = result as BadRequestObjectResult;
            Assert.NotNull(badRequestResult);

            var response = badRequestResult.Value as ErrorResponseDto;
            Assert.NotNull(response);
            Assert.False(response.Success);
            Assert.Equal("Token has already expired.", response.Message);
            Assert.Equal(ErrorCodes.ExpiredToken, response.ErrorCode);
            Assert.NotNull(response.TraceId);
        }



        private string GenerateExpiredToken()
        {
            var handler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes("ThisIsASuperLongSecretKeyThatHasEnoughLength123!");

            var now = DateTime.UtcNow;

            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Role, "User")
        }),
                NotBefore = now.AddMinutes(-20), // Czas rozpoczęcia ważności tokena (20 minut temu)
                Expires = now.AddMinutes(-10),  // Czas wygaśnięcia tokena (10 minut temu)
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = handler.CreateToken(descriptor);
            return handler.WriteToken(token);
        }


        [Fact]
        public async Task Logout_Should_Return_Ok_When_Token_Is_Revoked_Successfully()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            // Użycie istniejącej metody GenerateToken do wygenerowania poprawnego tokena
            var validToken = _tokenService.GenerateToken(userId: 1, role: "User");
            var expiryDate = DateTime.UtcNow.AddMinutes(10); // Czas wygaśnięcia tokena

            // Ustawienie nagłówka Authorization z poprawnym tokenem
            httpContext.Request.Headers["Authorization"] = $"Bearer {validToken}";

            // Act
            var result = await _controller.Logout();

            // Assert
            Assert.IsType<OkObjectResult>(result);

            var okResult = result as OkObjectResult;
            Assert.NotNull(okResult);

            var response = okResult.Value as SuccessResponseDto<object>;
            Assert.NotNull(response);
            Assert.True(response.Success);
            Assert.Equal("Logged out successfully.", response.Message);
            Assert.NotNull(response.TraceId);

            // Weryfikacja, że token został dodany do RevokedTokens
            var revokedToken = await _dbContext.RevokedTokens.FirstOrDefaultAsync(t => t.Token == validToken);
            Assert.NotNull(revokedToken);
            Assert.Equal(validToken, revokedToken.Token);
            Assert.True(revokedToken.ExpiryDate > DateTime.UtcNow);
        }



        [Fact]
        public async Task Logout_Should_Return_InternalServerError_When_Exception_Is_Thrown()
        {
            // Arrange

            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };


            // Użycie istniejącej metody GenerateToken do wygenerowania poprawnego tokena
            var validToken = _tokenService.GenerateToken(userId: 1, role: "User");
            var expiryDate = DateTime.UtcNow.AddMinutes(10); // Czas wygaśnięcia tokena

            // Ustawienie nagłówka Authorization z poprawnym tokenem
            httpContext.Request.Headers["Authorization"] = $"Bearer {validToken}";


            // Mockowanie bazy danych, aby rzucała wyjątek podczas zapisu
            _dbContext.Dispose();

            // Act
            var result = await _controller.Logout();

            // Assert
            Assert.IsType<ObjectResult>(result);

            var objectResult = result as ObjectResult;
            Assert.NotNull(objectResult);
            Assert.Equal(500, objectResult.StatusCode);

            var response = objectResult.Value as ErrorResponseDto;
            Assert.NotNull(response);
            Assert.False(response.Success);
            Assert.Equal("An error occurred while logging out. Please try again later.", response.Message);
            Assert.Equal(ErrorCodes.InternalServerError, response.ErrorCode);
            Assert.NotNull(response.TraceId);
        }


        #endregion
    }
}
