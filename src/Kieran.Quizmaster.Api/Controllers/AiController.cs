using System.Security.Claims;
using Kieran.Quizmaster.Application.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kieran.Quizmaster.Api.Controllers;

[ApiController]
[Route("ai")]
[Authorize]
public class AiController(IAiChatClientFactory factory) : ControllerBase
{
    /// <summary>
    /// Returns the AI providers the current user can actually use right now —
    /// Ollama if enabled server-side, OpenAI/Anthropic if the user has saved
    /// a key. The provider+model dropdowns on the New Quiz page bind to this.
    /// </summary>
    [HttpGet("providers")]
    public async Task<IActionResult> GetProviders(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var providers = await factory.GetAvailableProvidersAsync(userId, ct);
        return Ok(providers);
    }

    private bool TryGetUserId(out Guid userId)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out userId);
    }
}
