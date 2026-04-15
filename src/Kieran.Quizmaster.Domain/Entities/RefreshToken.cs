namespace Kieran.Quizmaster.Domain.Entities;

/// <summary>
/// One row per issued refresh token. We store a hash, not the raw value,
/// and chain rotations via <see cref="ReplacedByTokenId"/> so reuse of a
/// revoked token can be detected and the whole family revoked.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>SHA-256 of the raw token value.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>If non-null, this token was rotated and its successor's id is here.</summary>
    public Guid? ReplacedByTokenId { get; set; }
}
