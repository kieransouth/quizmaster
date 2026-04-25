namespace Kieran.Quizmaster.Application.Auth;

/// <summary>
/// Bound from the "Auth" config section. Holds runtime auth toggles —
/// distinct from <see cref="JwtOptions"/> which is JWT-specific config.
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// When false, /auth/register returns 403 and the frontend hides the
    /// "create account" UI. Existing accounts continue to work.
    /// </summary>
    public bool RegistrationEnabled { get; init; } = true;
}
