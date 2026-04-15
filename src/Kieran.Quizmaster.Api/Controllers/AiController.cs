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
    /// Returns the configured AI providers and per-provider model allowlists.
    /// Drives UI dropdowns; never includes API keys or other secrets.
    /// </summary>
    [HttpGet("providers")]
    public IActionResult GetProviders() => Ok(factory.GetAvailableProviders());
}
