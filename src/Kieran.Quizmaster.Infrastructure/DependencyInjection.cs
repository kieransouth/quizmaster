using Kieran.Quizmaster.Domain.Entities;
using Kieran.Quizmaster.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kieran.Quizmaster.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Wires up Infrastructure services: EF Core (Postgres) and ASP.NET Core
    /// Identity with Guid keys. Call once from Program.cs.
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
        // Phase 3 layers JWT auth on top via Microsoft.AspNetCore.Authentication.JwtBearer.
        services
            .AddIdentityCore<User>(options =>
            {
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        return services;
    }
}
