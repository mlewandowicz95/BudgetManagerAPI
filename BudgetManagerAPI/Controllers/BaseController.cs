using BudgetManagerAPI.Models;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BudgetManagerAPI.Controllers
{
    public class BaseController : ControllerBase
    {
        public int GetParseUserId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return 0;
            }


            int parsedUserId = int.Parse(userId);
            return parsedUserId;
        }
    }
}
