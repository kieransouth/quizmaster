using Kieran.Quizmaster.Application.Sessions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kieran.Quizmaster.Api.Controllers;

[ApiController]
[Route("share")]
[AllowAnonymous]
public class ShareController(ISessionService sessions) : ControllerBase
{
    /// <summary>
    /// Public, no-auth view of a completed session. The token is
    /// per-session (issued by SessionService.StartAsync) and only
    /// resolves while the session is Graded.
    /// </summary>
    [HttpGet("{token}")]
    public async Task<IActionResult> Get(string token, CancellationToken ct)
    {
        var summary = await sessions.GetByShareTokenAsync(token, ct);
        return summary is null ? NotFound() : Ok(summary);
    }
}
