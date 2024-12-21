using BudgetManagerAPI.Configurations;
using BudgetManagerAPI.Services;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BudgetManager.UnitTests.Services
{
    public class TokenServiceTests
    {
        [Fact]
        public void GenerateToken_ShouldReturnValidJwtToken()
        {
            var jwtSettings = new JwtSettings
            {
                SecretKey = "ThisIsASecretKeyForTesting1231232!",
                Issuer = "TestIssuer",
                Audience = "TestAudience",
                ExpiryInMinutes = 60
            };

            var tokenService = new TokenService(jwtSettings);
            var token = tokenService.GenerateToken(1, "test@example.com", "User");

            Assert.NotNull(token);
            var handler = new JwtSecurityTokenHandler();
            Assert.True(handler.CanReadToken(token)); // Check is token good

            var jwtToken = handler.ReadJwtToken(token);
            Assert.Equal("TestIssuer", jwtToken.Issuer);
            Assert.Equal("TestAudience", jwtToken.Audiences.First());
            Assert.Contains(jwtToken.Claims, c => c.Type == "UserId" && c.Value == "1");
        }
    }
}
