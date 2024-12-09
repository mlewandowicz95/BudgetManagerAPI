using BudgetManagerAPI.Controllers;
using BudgetManagerAPI.Data;
using BudgetManagerAPI.Enums;
using BudgetManagerAPI.Models;
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
    public class TransactionCategoryTests
    {
        private readonly Mock<ILogger<CategoryController>> _loggerMock;
        private readonly DbContextOptions<AppDbContext> _dbContextOptions;
        private readonly AppDbContext _dbContext;

        public TransactionCategoryTests()
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
            _dbContext.Transactions.AddRange(
                new Transaction
                {
                    UserId = 1,
                    CategoryId = 1,
                    Amount = -100.00m,
                    Date = DateTime.Now,
                    Description = "Kurs C#",
                    IsRecurring = false,
                    Type = TransactionType.Expense
                },
                new Transaction
                {
                    UserId = 1,
                    CategoryId = 2,
                    Amount = 3000.00m,
                    Date = new DateTime(new DateOnly(2024, 12, 05), new TimeOnly(15, 11)),
                    Description = "Etat",
                    IsRecurring = true,
                    Type = TransactionType.Income
                }
                );

            _dbContext.SaveChanges();
        }



        [Theory]
        [InlineData(TransactionType.Expense)]
        [InlineData(TransactionType.Income)]
        public void TransactionType_ShouldAcceptValidValues(TransactionType validType)
        {
            // Arrange
            var transaction = new Transaction
            {
                Id = 3,
                UserId = 1,
                Amount = 100.50m,
                Type = validType,
                Date = DateTime.Now,
            };

            Assert.Equal( validType, transaction.Type );
        }

    }
}
