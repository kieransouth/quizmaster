using Microsoft.AspNetCore.Http;

namespace Kieran.Quizmaster.Api.Auth;

internal static class AuthCookies
{
    public const string RefreshCookieName = "qm_refresh";

    /// <summary>
    /// Path covers both /api/auth/refresh and /api/auth/logout. The browser
    /// sees /api/* externally; Traefik strips /api before the API receives
    /// the request, but cookies are scoped to the browser's URL space.
    /// </summary>
    private const string CookiePath = "/api/auth";

    public static void SetRefreshCookie(HttpResponse response, string token, DateTimeOffset expiresAt)
    {
        response.Cookies.Append(RefreshCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure   = true,
            SameSite = SameSiteMode.Strict,
            Path     = CookiePath,
            Expires  = expiresAt,
        });
    }

    public static void ClearRefreshCookie(HttpResponse response)
    {
        response.Cookies.Append(RefreshCookieName, string.Empty, new CookieOptions
        {
            HttpOnly = true,
            Secure   = true,
            SameSite = SameSiteMode.Strict,
            Path     = CookiePath,
            Expires  = DateTimeOffset.UnixEpoch,
        });
    }
}
