var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// HTTPS termination is handled by Traefik in production. In dev,
// Vite proxies /api to plain http://localhost:5101.
app.UseAuthorization();
app.MapControllers();

app.Run();
