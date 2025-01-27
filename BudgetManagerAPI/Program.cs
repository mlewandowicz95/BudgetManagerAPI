
using BudgetManagerAPI.Configurations;
using BudgetManagerAPI.Data;
using BudgetManagerAPI.Enums;
using BudgetManagerAPI.Interfaces;
using BudgetManagerAPI.Middleware;
using BudgetManagerAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json.Serialization;

namespace BudgetManagerAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

            var builder = WebApplication.CreateBuilder(args);

            // Add Logging
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.AddDebug();
            // builder.Logging.AddFile("Logs/myapp-{Date}.txt"); - wymagany Serilog

            var secretKey = builder.Configuration["JwtSettings:SecretKey"];

            // CORS
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.WithOrigins("http://localhost:8080", "http://127.0.0.1:8080")
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });

            // Rejestracja konfiguracji JwtSettings
            builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
            builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<JwtSettings>>().Value);

            // Rejestracja ustawieñ i serwisu e-mail w DI
            builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
            builder.Services.AddScoped<IEmailService, EmailService>();

            // Add authentication
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
            {
                var secretKey = builder.Configuration["JwtSettings:SecretKey"];
                if (string.IsNullOrEmpty(secretKey))
                {
                    throw new InvalidOperationException("JwtSettings:SecretKey is not configured.");
                }


                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
                    ValidAudience = builder.Configuration["JwtSettings:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
                };
            });


            // Add services to the container.
            builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
            builder.Services.AddHostedService<CleanupRevokedTokenService>();
            builder.Services.AddLogging(); // logging
            builder.Services.AddScoped<TokenService>();
            builder.Services.AddScoped<AlertService>();
            builder.Services.AddScoped<IPdfReportService, PdfReportService>();
            builder.Services.AddScoped<AppDbContext>();
            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                });
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                // Mapowanie dla enum TransactionType
                c.MapType<TransactionType>(() => new OpenApiSchema
                {
                    Type = "string",
                    Enum = new List<IOpenApiAny>
        {
            new OpenApiString("Expense"),
            new OpenApiString("Income")
        }
                });
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. Example: 'Bearer {token}'",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
            });
            //foreach (var service in builder.Services)
            //{
            //    Console.WriteLine($"{service.ServiceType.FullName} -> {service.Lifetime}");
            //}
            //var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            //Console.WriteLine($"Connection String: {connectionString}");


            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseCors();

            app.UseHttpsRedirection();

            app.UseAuthentication();    // Add middleware to operate authentication
            app.UseAuthorization();
            app.UseMiddleware<TokenRevocationMiddleware>();
            app.Use(async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments("/confirm-email-change"))
                {
                    var newPath = $"http://localhost:8080{context.Request.Path}{context.Request.QueryString}";
                    Console.WriteLine($"Redirecting to: {newPath}");
                    context.Response.Redirect(newPath);
                    return;
                }

                if (context.Request.Path.StartsWithSegments("/reset-password"))
                {
                    var newPath = $"http://localhost:8080{context.Request.Path}{context.Request.QueryString}";
                    Console.WriteLine($"Redirecting to: {newPath}");
                    context.Response.Redirect(newPath);
                    return;
                }



                await next();
            });


            app.MapControllers();

            app.Run();
        }
    }
}
