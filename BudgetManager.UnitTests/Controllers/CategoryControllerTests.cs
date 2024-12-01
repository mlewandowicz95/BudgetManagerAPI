using BudgetManagerAPI.Controllers;
using BudgetManagerAPI.Data;
using BudgetManagerAPI.DTO;
using BudgetManagerAPI.Models;
using Castle.Core.Logging;
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
    public class CategoryControllerTests
    {
        private readonly Mock<ILogger<CategoryController>> _loggerMock;
        private readonly DbContextOptions<AppDbContext> _dbContextOptions;
        private readonly AppDbContext _dbContext;

        public CategoryControllerTests()
        {
            _loggerMock = new Mock<ILogger<CategoryController>>();

            _dbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _dbContext = new AppDbContext(_dbContextOptions);


            SeedDatabase();
        }

        public void Dispose()
        {
            _dbContext.Database.EnsureDeleted();
            _dbContext.Dispose();
        }

        private void SeedDatabase()
        {
            _dbContext.Categories.AddRange(
                new Category { Id = 1, Name = "Transport", UserId = 1 },
                new Category { Id = 2, Name = "Szkoła", UserId = 2 }
                );

            _dbContext.SaveChanges();
        }

        [Fact]
        public async Task GetCategories_ShouldReturnAllCategories()
        {
            // arrange
            var controlller = new CategoryController(_dbContext, _loggerMock.Object);

            // act
            var result = await controlller.GetCategories();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var categories = Assert.IsAssignableFrom<IEnumerable<CategoryResponseDto>>(okResult.Value);
            Assert.Equal(2, categories.Count());
        }

        // Test Mockowanego Loggera
        [Fact]
        public async Task GetCategories_ShouldLogInInformation()
        {

            // Arrange
            var loggerMock = new Mock<ILogger<CategoryController>>();
            var controller = new CategoryController(_dbContext, loggerMock.Object);

            // Act
            var result = await controller.GetCategories();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var categories = Assert.IsAssignableFrom<IEnumerable<CategoryResponseDto>>(okResult.Value);
            Assert.Equal(2, categories.Count());

            // Sprawdzenie logowania
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString() == "Fetching all categories."),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);

            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString() == "Successfully fetched 2 categories."),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetCategory_ShouldReturnCategory_WhenIdIsValid()
        {
            // arrange
            var controlller = new CategoryController(_dbContext, _loggerMock.Object);
            int id = 1;

            // Act
            var result = await controlller.GetCategory(id);

            // Assert
            var okResult = Assert.IsType<OkObjectResult >(result.Result);
            var category = Assert.IsType<CategoryResponseDto>(okResult.Value);
            Assert.Equal("Transport", category.Name);
        }

        [Fact]
        public async Task GetCategory_ShouldReturnNotFound_WhenIdIsInvalid()
        {
            // arrange
            var controlller = new CategoryController(_dbContext, _loggerMock.Object);
            int id = 99;

            // Act
            var result = await controlller.GetCategory(id);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result.Result);
        }


        [Fact]
        public async Task PostCategory_ShouldAddCategory()
        {
            // arrage 
            var controller = new CategoryController(_dbContext, _loggerMock.Object);
            var newCategory = new CategoryRequestDto
            {
                Name = "NewCategory",
                UserId = 3
            };

            // Act
            var result = await controller.PostCategory(newCategory);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var category = Assert.IsType<CategoryResponseDto>(createdResult.Value);
            Assert.Equal("NewCategory", category.Name);
        }

        [Fact]
        public async Task PutCategory_ShouldUpdateCategory_WhenIdIsValid()
        {
            // arrange 
            var controller = new CategoryController(_dbContext, _loggerMock.Object);
            int id = 1;
            var updatedCategory = new CategoryRequestDto
            {
                Name = "UpdatedTransport",
                UserId = 1
            };

            var result = await controller.PutCategory(id, updatedCategory);

            Assert.IsType<NoContentResult>(result);

            var category = _dbContext.Categories.First(c => c.Id == id);
            Assert.Equal("UpdatedTransport", category.Name);
        }


        [Fact]
        public async Task PutCategory_ShouldReturnNotFound_WhenIdIsInvalid()
        {
            var controller = new CategoryController(_dbContext, _loggerMock.Object);
            int id = 99;
            var updatedCategory = new CategoryRequestDto
            {
                Name = "InvalidCategory",
                UserId = 3
            };

            // Act
            var result = await controller.PutCategory(id, updatedCategory);

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task DeleteCategory_ShouldRemoveCategory_WhenIdIsValid()
        {
            var controller = new CategoryController(_dbContext, _loggerMock.Object);
            int id = 1;

            var result = await controller.DeleteCategory(id);

            // Assert
            Assert.IsType<NoContentResult> (result);
            Assert.Null(_dbContext.Categories.Find(id));
        }

        [Fact]
        public async Task DeleteCategory_ShouldReturnNotFound_WhenIdIsInvalid()
        {
            var controller = new CategoryController(_dbContext, _loggerMock.Object);
            int id = 99;

            var result = await controller.DeleteCategory(id);

            Assert.IsType<NotFoundObjectResult>(result);
        }
    }
}
