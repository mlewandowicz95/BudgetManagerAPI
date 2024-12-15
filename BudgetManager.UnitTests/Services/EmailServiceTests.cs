using BudgetManagerAPI.Configurations;
using BudgetManagerAPI.Services;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BudgetManager.UnitTests.Services
{
    public class EmailServiceTests
    {
        [Fact]
        public async Task SendEmail_ShouldWorkCorrectly()
        {
            var emailService = new EmailService(new OptionsWrapper<EmailSettings>(new EmailSettings
            {
                SmtpServer = "smtp.ethereal.email",
                Port = 587,
                SenderEmail = "trystan.schaefer@ethereal.email",
                SenderUsername = "trystan.schaefer@ethereal.email",
                SenderPassword = "VJTVJTfkrmrbJJUa29"
            }));

            await emailService.SendEmailAsync("recipient@example.com", "Test Subject", "Test Body");

            Assert.True(true); // Jeśli nie ma wyjątków, test przeszedł
        }
    }
}
