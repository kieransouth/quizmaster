using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Kieran.Quizmaster.Application.Auth;
using Kieran.Quizmaster.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Kieran.Quizmaster.Infrastructure.Auth;

public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : IJwtTokenService
{
    private readonly JwtOptions   _options = options.Value;
    private readonly TimeProvider _clock   = clock;

    public (string Token, DateTimeOffset ExpiresAt) IssueAccessToken(User user)
    {
        var now       = _clock.GetUtcNow();
        var expiresAt = now.Add(_options.AccessTokenLifetime);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new("displayName", user.DisplayName),
        };

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer:             _options.Issuer,
            audience:           _options.Audience,
            claims:             claims,
            notBefore:          now.UtcDateTime,
            expires:            expiresAt.UtcDateTime,
            signingCredentials: creds);

        var raw = new JwtSecurityTokenHandler().WriteToken(token);
        return (raw, expiresAt);
    }
}
