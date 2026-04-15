namespace Kieran.Quizmaster.Application.Auth.Dtos;

/// <summary>
/// Returned to the client on register/login/refresh. The access token is
/// short-lived; the refresh token is set as an httpOnly cookie and is NOT
/// included here so it never touches JS.
/// </summary>
public sealed record TokenPair(string AccessToken, DateTimeOffset AccessTokenExpiresAt, UserInfo User);
