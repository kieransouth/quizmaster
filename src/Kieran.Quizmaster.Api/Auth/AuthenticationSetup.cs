using System.Text;
using Kieran.Quizmaster.Application.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Kieran.Quizmaster.Api.Auth;

internal static class AuthenticationSetup
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration          configuration)
    {
        // We need the signing key here to wire the validator. Bind a local
        // copy from the same section so we don't depend on the IOptions
        // pipeline being ready at startup.
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                  ?? throw new InvalidOperationException("Jwt config section missing.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MapInboundClaims = false; // keep "sub" as "sub", not "nameidentifier"
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer           = true,
                    ValidIssuer              = jwt.Issuer,
                    ValidateAudience         = true,
                    ValidAudience            = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime         = true,
                    ClockSkew                = TimeSpan.Zero,
                    NameClaimType            = "sub",
                };
            });

        services.AddAuthorization();
        return services;
    }
}
