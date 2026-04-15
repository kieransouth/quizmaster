using Kieran.Quizmaster.Application.Ai;
using Kieran.Quizmaster.Application.Auth;
using Kieran.Quizmaster.Application.Quizzes;
using Kieran.Quizmaster.Domain.Entities;
using Kieran.Quizmaster.Infrastructure.Ai;
using Kieran.Quizmaster.Infrastructure.Ai.Quizzes;
using Kieran.Quizmaster.Infrastructure.Auth;
using Kieran.Quizmaster.Infrastructure.Persistence;
using Kieran.Quizmaster.Infrastructure.Quizzes;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kieran.Quizmaster.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Wires up Infrastructure services: EF Core (Postgres), ASP.NET Core
    /// Identity (Guid keys), and the JWT / refresh-token services. Call once
    /// from Program.cs.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Default is not configured.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        // IdentityCore = no cookies / no UI / no token providers we don't use.
        // JWT bearer auth (Phase 3) is layered on top by the API project.
        services
            .AddIdentityCore<User>(options =>
            {
                options.Password.RequiredLength         = 8;
                options.Password.RequireDigit           = false;
                options.Password.RequireUppercase       = false;
                options.Password.RequireLowercase       = false;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail         = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        // Auth services
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey) && o.SigningKey.Length >= 32,
                      "Jwt:SigningKey must be at least 32 characters.")
            .ValidateOnStart();

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();

        // AI services
        services.AddOptions<AiOptions>()
            .Bind(configuration.GetSection(AiOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IAiChatClientFactory, AiChatClientFactory>();

        // Quiz generation pipeline (Phase 5)
        services.AddScoped<IFactChecker,    FactChecker>();
        services.AddScoped<IQuizGenerator,  QuizGenerator>();
        services.AddScoped<IQuizImporter,   QuizImporter>();

        // Quiz persistence + edit (Phase 6)
        services.AddScoped<IQuizService,             QuizService>();
        services.AddScoped<IQuizQuestionRegenerator, QuizQuestionRegenerator>();

        return services;
    }
}
