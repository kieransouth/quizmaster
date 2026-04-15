namespace Kieran.Quizmaster.Application.Auth;

/// <summary>
/// Bound from the "Jwt" config section. SigningKey must be at least 32
/// bytes; AccessTokenLifetime is intentionally short (15 min) and refresh
/// tokens carry the long-lived session.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SigningKey { get; init; } = string.Empty;
    public string Issuer     { get; init; } = "quizmaster";
    public string Audience   { get; init; } = "quizmaster";

    public TimeSpan AccessTokenLifetime  { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan RefreshTokenLifetime { get; init; } = TimeSpan.FromDays(30);
}
