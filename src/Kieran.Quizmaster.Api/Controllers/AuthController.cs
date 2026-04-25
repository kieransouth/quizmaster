using System.Security.Claims;
using Kieran.Quizmaster.Api.Auth;
using Kieran.Quizmaster.Application.Auth;
using Kieran.Quizmaster.Application.Auth.Dtos;
using Kieran.Quizmaster.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Kieran.Quizmaster.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(
    UserManager<User>     userManager,
    IJwtTokenService      jwt,
    IRefreshTokenService  refreshTokens,
    IOptions<AuthOptions> authOptions) : ControllerBase
{
    [HttpGet("config")]
    [AllowAnonymous]
    public IActionResult Config() => Ok(new
    {
        registrationEnabled = authOptions.Value.RegistrationEnabled,
    });

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        if (!authOptions.Value.RegistrationEnabled)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = "Account registration is currently disabled." });
        }

        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return BadRequest(new { error = "Email, password, and display name are required." });
        }

        var user = new User
        {
            Id          = Guid.NewGuid(),
            UserName    = request.Email,
            Email       = request.Email,
            DisplayName = request.DisplayName,
        };

        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            return BadRequest(new { errors = created.Errors.Select(e => e.Description) });
        }

        return Ok(await IssuePairAsync(user, ct));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null) return Unauthorized();

        var ok = await userManager.CheckPasswordAsync(user, request.Password);
        if (!ok) return Unauthorized();

        return Ok(await IssuePairAsync(user, ct));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var raw = Request.Cookies[AuthCookies.RefreshCookieName];
        if (string.IsNullOrWhiteSpace(raw)) return Unauthorized();

        var result = await refreshTokens.RotateAsync(raw, ct);
        switch (result)
        {
            case RefreshRotationResult.Success(var user, var newRaw, var newExpires):
                AuthCookies.SetRefreshCookie(Response, newRaw, newExpires);
                var (access, accessExpires) = jwt.IssueAccessToken(user);
                return Ok(new TokenPair(access, accessExpires, ToUserInfo(user)));

            case RefreshRotationResult.ReuseDetected:
            case RefreshRotationResult.InvalidOrExpired:
            default:
                AuthCookies.ClearRefreshCookie(Response);
                return Unauthorized();
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var raw = Request.Cookies[AuthCookies.RefreshCookieName];
        if (!string.IsNullOrWhiteSpace(raw))
        {
            await refreshTokens.RevokeAsync(raw, ct);
        }
        AuthCookies.ClearRefreshCookie(Response);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId)) return Unauthorized();

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Unauthorized();

        return Ok(ToUserInfo(user));
    }

    private async Task<TokenPair> IssuePairAsync(User user, CancellationToken ct)
    {
        var (access, accessExpires) = jwt.IssueAccessToken(user);
        var (refresh, refreshExpires) = await refreshTokens.IssueAsync(user, ct);
        AuthCookies.SetRefreshCookie(Response, refresh, refreshExpires);
        return new TokenPair(access, accessExpires, ToUserInfo(user));
    }

    private static UserInfo ToUserInfo(User u) =>
        new(u.Id, u.Email ?? string.Empty, u.DisplayName);
}
