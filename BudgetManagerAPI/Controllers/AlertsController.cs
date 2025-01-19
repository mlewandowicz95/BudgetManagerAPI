using BudgetManagerAPI.Constants;
using BudgetManagerAPI.DTO;
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
            if (userId == 0)
            {
                return Unauthorized(new ErrorResponseDto
                {
                    Success = false,
                    TraceId = HttpContext.TraceIdentifier,
                    Message = "User is not authenticated.",
                    ErrorCode = ErrorCodes.Unathorized
                });
            }

            try
            {
                // Pobranie alertów dla użytkownika
                var alerts = await _alertService.GetUserAlerts(userId, onlyUnread);

                // Mapowanie na AlertResponseDto
                var alertDtos = alerts.Select(alert => new AlertResponseDto
                {
                    Id = alert.Id,
                    Message = alert.Message,
                    CreatedAt = alert.CreatedAt,
                    IsRead = alert.IsRead
                }).ToList();

                return Ok(new SuccessResponseDto<IEnumerable<AlertResponseDto>>
                {
                    Success = true,
                    Message = "User alerts fetched successfully.",
                    Data = alertDtos,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching alerts. TraceId: {TraceId}", HttpContext.TraceIdentifier);

                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Message = "An error occurred while processing your request.",
                    ErrorCode = ErrorCodes.InternalServerError,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }


        [HttpPost("mark-as-read")]
        public async Task<IActionResult> MarkAsRead([FromBody] List<int> alertIds)
        {
            if (alertIds == null || !alertIds.Any())
            {
                return BadRequest(new ErrorResponseDto
                {
                    Success = false,
                    Message = "Alert IDs cannot be empty.",
                    ErrorCode = ErrorCodes.InvalidData,
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            try
            {
                var userId = GetParseUserId();
                if (userId == 0)
                {
                    return Unauthorized(new ErrorResponseDto
                    {
                        Success = false,
                        Message = "User is not authenticated.",
                        ErrorCode = ErrorCodes.Unathorized,
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                // Oznaczenie alertów jako przeczytane
                var markedCount = await _alertService.MarkAsReadAsync(userId, alertIds);

                if (markedCount == 0)
                {
                    return NotFound(new ErrorResponseDto
                    {
                        Success = false,
                        Message = "No alerts found to mark as read.",
                        ErrorCode = ErrorCodes.NotFound,
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                return Ok(new SuccessResponseDto<int>
                {
                    Success = true,
                    Message = $"{markedCount} alert(s) marked as read.",
                    Data = markedCount,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while marking alerts as read. TraceId: {TraceId}", HttpContext.TraceIdentifier);

                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Message = "An error occurred while processing your request.",
                    ErrorCode = ErrorCodes.InternalServerError,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }

    }
}
