using Kieran.Quizmaster.Domain.Entities;

namespace Kieran.Quizmaster.Application.Auth;

public interface IRefreshTokenService
{
    /// <summary>Issues a new refresh token for a user and persists its hash.</summary>
    /// <returns>(rawToken, expiresAt). Hand the raw value to the caller; we only store the hash.</returns>
    Task<(string RawToken, DateTimeOffset ExpiresAt)> IssueAsync(User user, CancellationToken ct);

    /// <summary>
    /// Validates a presented refresh token and, on success, rotates it: marks the old one
    /// revoked + linked to its successor, returns the new raw token.
    /// </summary>
    Task<RefreshRotationResult> RotateAsync(string rawToken, CancellationToken ct);

    /// <summary>Logout: revoke the presented refresh token if it exists and is currently active.</summary>
    Task RevokeAsync(string rawToken, CancellationToken ct);
}
