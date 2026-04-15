using Kieran.Quizmaster.Domain.Entities;

namespace Kieran.Quizmaster.Application.Auth;

/// <summary>
/// Outcomes of presenting a refresh token to be rotated.
/// </summary>
public abstract record RefreshRotationResult
{
    /// <summary>The token rotated cleanly. Issue a new access token + set the new refresh cookie.</summary>
    public sealed record Success(User User, string NewRefreshToken, DateTimeOffset NewRefreshExpiresAt)
        : RefreshRotationResult;

    /// <summary>
    /// A previously-rotated (revoked) token was presented again. We assume an attacker stole it,
    /// revoke the whole family for that user, and force re-login.
    /// </summary>
    public sealed record ReuseDetected(Guid UserId) : RefreshRotationResult;

    /// <summary>Unknown, expired, or otherwise unusable token. Force re-login.</summary>
    public sealed record InvalidOrExpired : RefreshRotationResult;
}
