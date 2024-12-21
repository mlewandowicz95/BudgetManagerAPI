using BudgetManagerAPI.Data;
using BudgetManagerAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace BudgetManagerAPI.Services
{
    public class AlertService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AlertService> _logger;

        public AlertService(AppDbContext context, ILogger<AlertService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task CreateAlert(int userId, string message)
        {
            var alert = new Alert
            {
                UserId = userId,
                Message = message,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.Alerts.Add(alert);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Alert>> GetUserAlerts(int userId, bool onlyUnread = false)
        {
            var query = _context.Alerts.Where(a => a.UserId ==  userId);

            if(onlyUnread)
            {
                query = query.Where(a => !a.IsRead);
            }

            return await query.ToListAsync();
        }

        public async Task MarkAlertAsRead(int alertId)
        {
            var alert = await _context.Alerts.FindAsync(alertId);

            if(alert != null)
            {
                alert.IsRead = true;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<int> MarkAsReadAsync(int userId, List<int> alertIds)
        {
            var alerts = await _context.Alerts
                .Where(a => alertIds.Contains(a.Id) && a.UserId == userId && !a.IsRead).ToListAsync();

            if (alerts.Count == 0)
                return 0;

            alerts.ForEach(a => a.IsRead = true);

            try
            {
                await _context.SaveChangesAsync();
                return alerts.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to mark alerts as read.");
                throw new InvalidOperationException("Failed to mark alerts as read.", ex);
            }


        }
    }
}
