
using BudgetManagerAPI.Data;

namespace BudgetManagerAPI.Services
{
    public class CleanupRevokedTokenService : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<CleanupRevokedTokenService> _logger;

        public CleanupRevokedTokenService(IServiceScopeFactory serviceScopeFactory, ILogger<CleanupRevokedTokenService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        public async Task CleanupExpiredTokensAsync()
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var expiredTokens = dbContext.RevokedTokens
                .Where(token => token.ExpiryDate < DateTime.UtcNow);

            if (expiredTokens.Any())
            {
                _logger.LogInformation($"Found {expiredTokens.Count()} expired tokens. Cleaning...");
                dbContext.RevokedTokens.RemoveRange(expiredTokens);
                await dbContext.SaveChangesAsync();
                _logger.LogInformation("Expired tokens cleaned.");
            }
            else
            {
                _logger.LogInformation("No expired tokens found.");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await CleanupExpiredTokensAsync();
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken); // Harmonogram
            }
        }
    }

}
