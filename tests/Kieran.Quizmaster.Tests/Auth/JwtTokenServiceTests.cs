using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Kieran.Quizmaster.Application.Auth;
using Kieran.Quizmaster.Domain.Entities;
using Kieran.Quizmaster.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Tokens;
using Shouldly;

namespace Kieran.Quizmaster.Tests.Auth;

public class JwtTokenServiceTests
{
    private const string SigningKey = "test-signing-key-must-be-at-least-32-chars-long";

    private static (JwtTokenService Sut, FakeTimeProvider Clock, JwtOptions Opts) Build(TimeSpan? lifetime = null)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 4, 15, 12, 0, 0, TimeSpan.Zero));
        var opts  = new JwtOptions
        {
            SigningKey          = SigningKey,
            Issuer              = "qm-test",
            Audience            = "qm-test",
            AccessTokenLifetime = lifetime ?? TimeSpan.FromMinutes(15),
        };
        var sut = new JwtTokenService(Options.Create(opts), clock);
        return (sut, clock, opts);
    }

    private static User UserFor(Guid id) => new()
    {
        Id          = id,
        Email       = "kieran@example.test",
        UserName    = "kieran@example.test",
        DisplayName = "Kieran",
    };

    [Fact]
    public void Issues_token_with_expected_claims()
    {
        var (sut, _, opts) = Build();
        var userId = Guid.NewGuid();

        var (raw, _) = sut.IssueAccessToken(UserFor(userId));

        var token = new JwtSecurityTokenHandler().ReadJwtToken(raw);
        token.Issuer.ShouldBe(opts.Issuer);
        token.Audiences.ShouldContain(opts.Audience);
        token.Claims.ShouldContain(c => c.Type == "sub"         && c.Value == userId.ToString());
        token.Claims.ShouldContain(c => c.Type == "email"       && c.Value == "kieran@example.test");
        token.Claims.ShouldContain(c => c.Type == "displayName" && c.Value == "Kieran");
        token.Claims.ShouldContain(c => c.Type == "jti");
    }

    [Fact]
    public void Sets_expiry_to_now_plus_lifetime()
    {
        var (sut, clock, opts) = Build(TimeSpan.FromMinutes(15));
        var (_, expiresAt) = sut.IssueAccessToken(UserFor(Guid.NewGuid()));

        expiresAt.ShouldBe(clock.GetUtcNow().Add(opts.AccessTokenLifetime));
    }

    [Fact]
    public void Token_validates_with_configured_signing_key()
    {
        var (sut, _, opts) = Build();
        var (raw, _) = sut.IssueAccessToken(UserFor(Guid.NewGuid()));

        var handler = new JwtSecurityTokenHandler();
        Should.NotThrow(() => handler.ValidateToken(raw, new TokenValidationParameters
        {
            ValidateIssuer           = true,  ValidIssuer = opts.Issuer,
            ValidateAudience         = true,  ValidAudience = opts.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            ValidateLifetime         = false, // we test exp separately
        }, out _));
    }

    [Fact]
    public void Token_does_not_validate_with_a_different_signing_key()
    {
        var (sut, _, opts) = Build();
        var (raw, _) = sut.IssueAccessToken(UserFor(Guid.NewGuid()));

        var handler = new JwtSecurityTokenHandler();
        Should.Throw<SecurityTokenInvalidSignatureException>(() => handler.ValidateToken(raw, new TokenValidationParameters
        {
            ValidateIssuer           = true,  ValidIssuer = opts.Issuer,
            ValidateAudience         = true,  ValidAudience = opts.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("a-completely-different-key-that-is-32+")),
            ValidateLifetime         = false,
        }, out _));
    }
}
