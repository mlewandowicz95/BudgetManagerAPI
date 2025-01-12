using Azure;
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
using System.Linq.Expressions;
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
            Assert.False(errorResponse.Success, "Response should not indicate success.");

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

            Assert.Equal("MISSING_TOKEN", errorResponse.ErrorCode);
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

            Assert.Equal("INVALID_ACTIVATION_TOKEN", errorResponse.ErrorCode);
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

            Assert.Equal("USER_ALREADY_ACTIVE", errorResponse.ErrorCode);
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
            Assert.Equal("INTERNAL_SERVER_ERROR", errorResponse.ErrorCode);
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
            Assert.Equal("VALIDATION_ERROR", response.ErrorCode);
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
            Assert.Equal("USER_NOT_FOUND", response.ErrorCode);
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
            Assert.Equal("USER_ALREADY_ACTIVE", response.ErrorCode);
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
            Assert.Equal("INTERNAL_SERVER_ERROR", errorResponse.ErrorCode);

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
    }
}
