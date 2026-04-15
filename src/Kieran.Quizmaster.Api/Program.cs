using Kieran.Quizmaster.Api.Auth;
using Kieran.Quizmaster.Infrastructure;
using Kieran.Quizmaster.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Apply pending migrations on startup. Single-container self-host: this is
// fine. If migration fails we crash loudly so the operator sees it instead
// of the API silently serving against a stale schema.
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("Startup");
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.Migrate();
        logger.LogInformation("Database migrations applied.");
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Database migration failed; aborting startup.");
        throw;
    }
}

// HTTPS termination is handled by Traefik in production. In dev, Vite
// proxies /api to plain http://localhost:5101.
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
