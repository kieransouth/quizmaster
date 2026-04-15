using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace Kieran.Quizmaster.Api.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private static readonly string Version =
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
        ?? "unknown";

    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        status = "ok",
        version = Version,
    });
}
