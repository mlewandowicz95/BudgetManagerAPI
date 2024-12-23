using BudgetManagerAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BudgetManagerAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/alerts")]
    public class AlertsController : BaseController
    {
        private readonly AlertService _alertService;
        private readonly ILogger<AlertsController> _logger;

        public AlertsController(AlertService alertService, ILogger<AlertsController> logger)
        {
            _alertService = alertService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetUserAlerts([FromQuery] bool onlyUnread = false)
        {
            var userId = GetParseUserId();
            if(userId == 0)
                return Unauthorized(new {Message = "Error UserId"});

            try
            {
                var alerts = await _alertService.GetUserAlerts(userId, onlyUnread);

                return Ok(alerts);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "An error occured while GetAlerts.");
                return StatusCode(50, new { Message = "Error occured while processing your request." });
            }
        }

        [HttpPost("mark-as-read")]
        public async Task<IActionResult> MarkAsRead([FromBody] List<int> alertIds)
        {

            if (alertIds == null || !alertIds.Any())
            {
                return BadRequest(new { Message = "Alert IDs cannot be empty." });
            }

            try
            {
                var userId = GetParseUserId();
                if (userId == 0)
                    return Unauthorized(new { Message = "Error UserId" });

                var markedCount = await _alertService.MarkAsReadAsync(userId, alertIds);

                if(markedCount == 0)
                {
                    return NotFound(new { Message = "No alerts found to mark as read." });
                }

                return Ok(new { Message = $"{markedCount} alert(s) marked as read." });
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "An error occured while marking alerts as read.");
                return StatusCode(500, new { Message = "An error occured while processing your request." });
            }
        }
    }
}
