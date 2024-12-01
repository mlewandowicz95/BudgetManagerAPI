using BudgetManagerAPI.Controllers;
using BudgetManagerAPI.Data;
using BudgetManagerAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace BudgetManager.UnitTests.Controllers
{
    public class UserControllerTests
    {

        private static Mock<DbSet<T>> CreateMockDbSet<T>(IQueryable<T> data) where T : class
        {
            var mockDbSet = new Mock<DbSet<T>>();
            mockDbSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(data.Provider);
            mockDbSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(data.Expression);
            mockDbSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(data.ElementType);
            mockDbSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(data.GetEnumerator());
            return mockDbSet;
        }

        [Fact]
        public async Task GetUser_ReturnsUser_WhenUserExists()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDatabase")
                .Options;

            using var context = new AppDbContext(options);

            // Dodaj użytkownika do bazy
            var user = new User { Id = 1, Email = "test@example.com", PasswordHash ="aszjhjhsdfhj23f$$$" };
            context.Users.Add(user);
            context.SaveChanges();

            var controller = new UserController(context);

            // Act
            var result = await controller.GetUser(1);

            // Assert
            var actionResult = Assert.IsType<ActionResult<User>>(result);
            var returnedUser = Assert.IsType<User>(actionResult.Value);
            Assert.Equal(user.Email, returnedUser.Email);
        }


        [Fact]
        public async Task GetUser_ReturnsNotFound_WhenUserDoesNotExist()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDatabase")
                .Options;

            using var context = new AppDbContext(options);
            var controller = new UserController(context);

            // Act
            var result = await controller.GetUser(99);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task PostUser_ReturnsCreatedUser_WhenValidInput()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDatabase_PostUser")
                .Options;

            using var context = new AppDbContext(options);
            var newUser = new User { Email = "newuser@example.com", PasswordHash="123hjhfdsuj234" };
            var controller = new UserController(context);

            // Act
            var result = await controller.PostUser(newUser);

            // Assert
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var createdUser = Assert.IsType<User>(createdAtActionResult.Value);

            // Sprawdź, czy zwrócony użytkownik ma poprawny e-mail
            Assert.Equal(newUser.Email, createdUser.Email);

            // Sprawdź, czy użytkownik został zapisany w bazie danych
            var savedUser = context.Users.FirstOrDefault(u => u.Email == newUser.Email);
            Assert.NotNull(savedUser);
            Assert.Equal(newUser.Email, savedUser.Email);
        }

        [Fact]
        public async Task PostUser_ReturnsBadRequest_WhenEmailIsInvalid()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDatabase_InvalidEmail")
                .Options;


            using var context = new AppDbContext(options);
            var newUser = new User { Email = "invalid-email", PasswordHash="12321hj3" }; //Niepoprawny email 
            var controller = new UserController(context);
             controller.ModelState.AddModelError("email", "Email is failed");
      
            // Act
            var result = await controller.PostUser(newUser);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetUser_ReturnsInternalServerError_WhenExceptionIsThrown()
        {
            // Tworzymy pustą listę użytkowników (symulacja danych)
            var users = new List<User>().AsQueryable();
            var mockDbSet = CreateMockDbSet(users);

            // Symulacja rzucania wyjątku
            mockDbSet.Setup(m => m.FindAsync(It.IsAny<int>()))
                     .ThrowsAsync(new Exception("Database error"));

            // Zamockowanie AppDbContext
            var mockDbContext = new Mock<AppDbContext>();
            mockDbContext.Setup(c => c.Users).Returns(mockDbSet.Object);

            var controller = new UserController(mockDbContext.Object);

            // Act
            var result = await controller.GetUser(1);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(500, objectResult.StatusCode);
        }





        /* MOQ
        [Fact]
        public async Task PostUser_ReturnsCreatedUser_WhenValidInput()
        {
            // Arrange
            var mockDbSet = new Mock<DbSet<User>>();
            var mockDbContext = new Mock<AppDbContext>();
            mockDbContext.Setup(c => c.Users).Returns(mockDbSet.Object);

            var newUser = new User { Email = "newuser@example.com", PasswordHash ="123128fgbhergf1!!" };

            // Utwórz mockowany UserController
            var controller = new UserController(mockDbContext.Object);

            // Act
            var result = await controller.PostUser(newUser);

            // Assert
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var createdUser = Assert.IsType<User>(createdAtActionResult.Value);

            // Weryfikacja, że właściwości użytkownika są poprawne
            Assert.Equal(newUser.Email, createdUser.Email);

            // Weryfikacja, że metoda Add została wywołana raz
            mockDbSet.Verify(m => m.Add(It.IsAny<User>()), Times.Once);

            // Weryfikacja, że metoda SaveChangesAsync została wywołana raz
            mockDbContext.Verify(m => m.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }


        */


    }
}
