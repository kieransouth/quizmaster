using System.Security.Claims;
using Kieran.Quizmaster.Application.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kieran.Quizmaster.Api.Controllers;

[ApiController]
[Route("me")]
[Authorize]
public class MeController(IUserApiKeyService apiKeys) : ControllerBase
{
    /// <summary>
    /// List the current user's saved-key status, one row per server-configured
    /// (and enabled) provider. Never returns the plaintext key — only a
    /// masked preview when one is set.
    /// </summary>
    [HttpGet("ai-providers")]
    public async Task<IActionResult> ListProviders(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var statuses = await apiKeys.ListAsync(userId, ct);
        return Ok(statuses);
    }

    /// <summary>Set or replace the user's API key for one provider.</summary>
    [HttpPut("ai-providers/{provider}")]
    public async Task<IActionResult> SetKey(
        string provider,
        [FromBody] SetKeyRequest body,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(body.ApiKey))
            return BadRequest(new { error = "apiKey is required." });

        try
        {
            await apiKeys.SetKeyAsync(userId, provider, body.ApiKey, ct);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        return NoContent();
    }

    /// <summary>Clear the user's API key for one provider.</summary>
    [HttpDelete("ai-providers/{provider}")]
    public async Task<IActionResult> RemoveKey(string provider, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        await apiKeys.RemoveKeyAsync(userId, provider, ct);
        return NoContent();
    }

    private bool TryGetUserId(out Guid userId)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out userId);
    }

    public record SetKeyRequest(string ApiKey);
}
