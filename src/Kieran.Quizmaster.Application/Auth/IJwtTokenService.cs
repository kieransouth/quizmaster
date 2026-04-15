using Kieran.Quizmaster.Domain.Entities;

namespace Kieran.Quizmaster.Application.Auth;

public interface IJwtTokenService
{
    /// <summary>
    /// Issues a signed access token for the given user.
    /// </summary>
    /// <returns>(token, expiresAt) — expiresAt mirrors the JWT 'exp' claim.</returns>
    (string Token, DateTimeOffset ExpiresAt) IssueAccessToken(User user);
}
