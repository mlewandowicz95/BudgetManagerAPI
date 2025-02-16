﻿using BudgetManagerAPI.Constants;
using BudgetManagerAPI.Data;
using BudgetManagerAPI.DTO;
using Microsoft.EntityFrameworkCore;

namespace BudgetManagerAPI.Middleware
{
    public class TokenRevocationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TokenRevocationMiddleware> _logger;

        public TokenRevocationMiddleware(RequestDelegate next, ILogger<TokenRevocationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IServiceScopeFactory scopeFactory)
        {
            try
            {
                _logger.LogInformation($"Processing request: {context.Request.Path}");

                // Wyjątki dla endpointów, które nie wymagają autoryzacji
                if (context.Request.Path.StartsWithSegments("/login", StringComparison.OrdinalIgnoreCase) ||
                    context.Request.Path.StartsWithSegments("/logout", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation($"Skipping middleware for {context.Request.Path} endpoint.");
                    await _next(context);
                    return;
                }


                // Pobranie nagłówka Authorization
                var authHeader = context.Request.Headers["Authorization"].ToString();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    _logger.LogWarning("Missing or invalid Authorization header.");
                    await _next(context); // Kontynuuj przetwarzanie, jeśli brak tokena
                    return;
                }

                var token = authHeader.Replace("Bearer ", "").Trim();
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("Empty token after removing Bearer prefix.");
                    await _next(context); // Kontynuuj przetwarzanie, jeśli token jest pusty
                    return;
                }

                using var scope = scopeFactory.CreateScope(); // Tworzenie lokalnego zasięgu
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Sprawdzenie, czy token jest odwołany
                var isRevoked = await dbContext.RevokedTokens.AnyAsync(t => t.Token == token);
                if (isRevoked)
                {
                    _logger.LogWarning("Revoked token detected: {Token}", token);

                    var errorResponse = new ErrorResponseDto
                    {
                        Success = false,
                        Message = "Token has been revoked.",
                        TraceId = context.TraceIdentifier,
                        ErrorCode = ErrorCodes.Unathorized,
                        Errors = new Dictionary<string, string[]>
        {
            { "Token", new[] { "The provided token is no longer valid." } }
        }
                    };

                    context.Response.StatusCode = StatusCodes.Status401Unauthorized; // 401 Unauthorized
                    context.Response.ContentType = "application/json";

                    var jsonResponse = System.Text.Json.JsonSerializer.Serialize(errorResponse);
                    await context.Response.WriteAsync(jsonResponse);

                    return;
                }


                // Kontynuuj przetwarzanie, jeśli token nie jest odwołany
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TokenRevocationMiddleware");
                throw;
            }
        }
    }
}
