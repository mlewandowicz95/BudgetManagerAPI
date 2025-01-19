using BudgetManagerAPI.Constants;
using BudgetManagerAPI.Data;
using BudgetManagerAPI.DTO;
using BudgetManagerAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace BudgetManagerAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoryController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CategoryController> _logger;

        public CategoryController(AppDbContext context, ILogger<CategoryController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetCategories()
        {
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

                IQueryable<Category> query = _context.Categories;

                if (!User.IsInRole(Roles.Admin))
                {
                    // Ograniczenie widoczności dla ról innych niż Admin
                    query = query.Where(c => c.UserId == null || c.UserId == userId);
                }

                var categories = await query
                    .Select(category => new CategoryResponseDto
                    {
                        Id = category.Id,
                        Name = category.Name,
                        UserId = category.UserId
                    })
                    .ToListAsync();

                _logger.LogInformation("Successfully fetched {Count} categories for user {UserId}. TraceId: {TraceId}", categories.Count, userId, HttpContext.TraceIdentifier);

                return Ok(new SuccessResponseDto<List<CategoryResponseDto>>
                {
                    Success = true,
                    Message = $"Successfully fetched {categories.Count} categories.",
                    Data = categories,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching categories. TraceId: {TraceId}", HttpContext.TraceIdentifier);

                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Message = "An error occurred while processing your request.",
                    ErrorCode = ErrorCodes.InternalServerError,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }



        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetCategory(int id)
        {
            try
            {
                if (id <= 0)
                {
                    return BadRequest(new ErrorResponseDto
                    {
                        Success = false,
                        Message = "Invalid category ID.",
                        ErrorCode = ErrorCodes.InvalidId,
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                var category = await _context.Categories.FindAsync(id);
                if (category == null)
                {
                    _logger.LogWarning("Category with ID {Id} not found. TraceId: {TraceId}", id, HttpContext.TraceIdentifier);
                    return NotFound(new ErrorResponseDto
                    {
                        Success = false,
                        Message = $"Category with ID {id} not found.",
                        ErrorCode = ErrorCodes.NotFound,
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

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

                if (!User.IsInRole(Roles.Admin) && category.UserId == null)
                {
                    _logger.LogWarning("Access denied for user {UserId} to category {CategoryId}. TraceId: {TraceId}", userId, id, HttpContext.TraceIdentifier);
                    return new ObjectResult(new ErrorResponseDto
                    {
                        Success = false,
                        Message = "You do not have permission to access this category.",
                        TraceId = HttpContext.TraceIdentifier,
                        ErrorCode = ErrorCodes.Forbidden,
                        Errors = new Dictionary<string, string[]>
        {
            { "Authorization", new[]
                {
                    $"User with ID {userId} attempted to access a global category with ID {id}.",
                    "Only Admins can access global categories."
                }
            }
        }
                    })
                    {
                        StatusCode = StatusCodes.Status403Forbidden
                    };
                }


                var categoryResponseDto = new CategoryResponseDto
                {
                    Id = category.Id,
                    Name = category.Name,
                    UserId = category.UserId
                };

                _logger.LogInformation("Category with ID {Id} fetched successfully. TraceId: {TraceId}", id, HttpContext.TraceIdentifier);

                return Ok(new SuccessResponseDto<CategoryResponseDto>
                {
                    Success = true,
                    Message = $"Category with ID {id} fetched successfully.",
                    Data = categoryResponseDto,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching category with ID {Id}. TraceId: {TraceId}", id, HttpContext.TraceIdentifier);

                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Message = "An error occurred while processing your request.",
                    ErrorCode = ErrorCodes.InternalServerError,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }



        // POST: api/Category
        [Authorize(Roles = $"{Roles.Admin},{Roles.Pro}")]
        [HttpPost]
        public async Task<IActionResult> PostCategory([FromBody] CategoryRequestDto categoryRequestDto)
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

            if (!ModelState.IsValid)
            {
                return BadRequest(new ErrorResponseDto
                {
                    Success = false,
                    Message = "Invalid request data.",
                    ErrorCode = ErrorCodes.Unathorized,
                    TraceId = HttpContext.TraceIdentifier,
                    Errors = ModelState.ToDictionary(
                        key => key.Key,
                        value => value.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                    )
                });
            }

            // Pro użytkownicy mogą tworzyć kategorie tylko dla siebie
            if (User.IsInRole(Roles.Pro) && categoryRequestDto.UserId != userId)
            {
                return new ObjectResult(new ErrorResponseDto
                {
                    Success = false,
                    Message = "Pro users can only create categories for themselves.",
                    TraceId = HttpContext.TraceIdentifier,
                    ErrorCode = ErrorCodes.Forbidden,
                    Errors = new Dictionary<string, string[]>
            {
                { "Authorization", new[] { $"User with ID {userId} attempted to create a category for another user with ID {categoryRequestDto.UserId}." } }
            }
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }

            // Tworzenie kategorii globalnej przez admina
            if (User.IsInRole(Roles.Admin) && categoryRequestDto.UserId == null)
            {
                var globalCategory = new Category
                {
                    Name = categoryRequestDto.Name,
                    UserId = null,
                };

                try
                {
                    _context.Categories.Add(globalCategory);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Admin user {UserId} created global category {CategoryId}. TraceId: {TraceId}", userId, globalCategory.Id, HttpContext.TraceIdentifier);

                    return CreatedAtAction("GetCategory", new { id = globalCategory.Id }, new SuccessResponseDto<CategoryResponseDto>
                    {
                        Success = true,
                        Message = "Global category created successfully.",
                        Data = new CategoryResponseDto
                        {
                            Id = globalCategory.Id,
                            Name = globalCategory.Name,
                            UserId = globalCategory.UserId
                        },
                        TraceId = HttpContext.TraceIdentifier
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while creating a global category. TraceId: {TraceId}", HttpContext.TraceIdentifier);

                    return StatusCode(500, new ErrorResponseDto
                    {
                        Success = false,
                        Message = "An error occurred while processing your request.",
                        ErrorCode = ErrorCodes.InternalServerError,
                        TraceId = HttpContext.TraceIdentifier
                    });
                }
            }

            // Tworzenie kategorii użytkownika
            var category = new Category
            {
                Name = categoryRequestDto.Name,
                UserId = categoryRequestDto.UserId
            };

            try
            {
                _context.Categories.Add(category);
                await _context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} created category {CategoryId}. TraceId: {TraceId}", userId, category.Id, HttpContext.TraceIdentifier);

                return CreatedAtAction("GetCategory", new { id = category.Id }, new SuccessResponseDto<CategoryResponseDto>
                {
                    Success = true,
                    Message = "Category created successfully.",
                    Data = new CategoryResponseDto
                    {
                        Id = category.Id,
                        Name = category.Name,
                        UserId = category.UserId
                    },
                    TraceId = HttpContext.TraceIdentifier
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating a category. TraceId: {TraceId}", HttpContext.TraceIdentifier);

                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Message = "An error occurred while processing your request.",
                    ErrorCode = ErrorCodes.InternalServerError,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }



        // PUT: api/Category/2
        [HttpPut("{id}")]
        [Authorize(Roles = $"{Roles.Admin},{Roles.Pro}")]
        public async Task<IActionResult> PutCategory(int id, [FromBody] CategoryRequestDto dto)
        {
            if (id <= 0)
            {
                return BadRequest(new ErrorResponseDto
                {
                    Success = false,
                    Message = "Invalid category ID.",
                    ErrorCode = ErrorCodes.InvalidId,
                    TraceId = HttpContext.TraceIdentifier
                });
            }

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

            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                _logger.LogWarning("Category with ID {Id} not found. TraceId: {TraceId}", id, HttpContext.TraceIdentifier);
                return NotFound(new ErrorResponseDto
                {
                    Success = false,
                    Message = $"Category with ID {id} not found.",
                    ErrorCode = ErrorCodes.NotFound,
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            if (User.IsInRole(Roles.Admin))
            {
                // Admin może edytować wszystkie kategorie
                category.Name = dto.Name;
                category.UserId = dto.UserId;
            }
            else if (category.UserId == userId)
            {
                // Użytkownicy Pro mogą edytować tylko własne kategorie
                category.Name = dto.Name;
            }
            else
            {
                _logger.LogWarning("User {UserId} attempted to edit category {CategoryId} without permission. TraceId: {TraceId}", userId, id, HttpContext.TraceIdentifier);
                return new ObjectResult(new ErrorResponseDto
                {
                    Success = false,
                    Message = "You can only edit your own categories.",
                    TraceId = HttpContext.TraceIdentifier,
                    ErrorCode = ErrorCodes.Forbidden,
                    Errors = new Dictionary<string, string[]>
            {
                { "Authorization", new[] { $"User with ID {userId} attempted to edit category with ID {id}." } }
            }
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }

            try
            {
                await _context.SaveChangesAsync();

                _logger.LogInformation("Category with ID {Id} updated successfully. TraceId: {TraceId}", id, HttpContext.TraceIdentifier);

                return Ok(new SuccessResponseDto<CategoryResponseDto>
                {
                    Success = true,
                    Message = $"Category with ID {id} updated successfully.",
                    TraceId = HttpContext.TraceIdentifier,
                    Data = new CategoryResponseDto
                    {
                        Id = category.Id,
                        Name = category.Name,
                        UserId = category.UserId,
                    }
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning("Concurrency conflict while updating category with ID {Id}. TraceId: {TraceId}", id, HttpContext.TraceIdentifier);

                return Conflict(new ErrorResponseDto
                {
                    Success = false,
                    Message = "Concurrency conflict occurred while updating the category.",
                    ErrorCode = ErrorCodes.Conflict,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating category with ID {Id}. TraceId: {TraceId}", id, HttpContext.TraceIdentifier);

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
