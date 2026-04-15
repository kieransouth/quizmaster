using System.Security.Cryptography;
using System.Text;
using Kieran.Quizmaster.Application.Auth;
using Kieran.Quizmaster.Domain.Entities;
using Kieran.Quizmaster.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kieran.Quizmaster.Infrastructure.Auth;

public sealed class RefreshTokenService(
    ApplicationDbContext db,
    IOptions<JwtOptions>  options,
    TimeProvider          clock) : IRefreshTokenService
{
    private readonly ApplicationDbContext _db      = db;
    private readonly JwtOptions           _options = options.Value;
    private readonly TimeProvider         _clock   = clock;

    public async Task<(string RawToken, DateTimeOffset ExpiresAt)> IssueAsync(User user, CancellationToken ct)
    {
        var now       = _clock.GetUtcNow();
        var expiresAt = now.Add(_options.RefreshTokenLifetime);
        var raw       = GenerateRawToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            Id        = Guid.NewGuid(),
            UserId    = user.Id,
            TokenHash = HashToken(raw),
            CreatedAt = now,
            ExpiresAt = expiresAt,
        });
        await _db.SaveChangesAsync(ct);

        return (raw, expiresAt);
    }

    public async Task<RefreshRotationResult> RotateAsync(string rawToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return new RefreshRotationResult.InvalidOrExpired();

        var hash = HashToken(rawToken);
        var now  = _clock.GetUtcNow();

        var existing = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (existing is null)
            return new RefreshRotationResult.InvalidOrExpired();

        if (existing.ExpiresAt <= now)
            return new RefreshRotationResult.InvalidOrExpired();

        if (existing.RevokedAt is not null)
        {
            // Reuse of a revoked token — assume credential theft, revoke the whole family.
            await RevokeAllForUserAsync(existing.UserId, now, ct);
            return new RefreshRotationResult.ReuseDetected(existing.UserId);
        }

        // Issue successor + chain
        var newRaw       = GenerateRawToken();
        var newExpiresAt = now.Add(_options.RefreshTokenLifetime);
        var successor    = new RefreshToken
        {
            Id        = Guid.NewGuid(),
            UserId    = existing.UserId,
            TokenHash = HashToken(newRaw),
            CreatedAt = now,
            ExpiresAt = newExpiresAt,
        };

        existing.RevokedAt         = now;
        existing.ReplacedByTokenId = successor.Id;
        _db.RefreshTokens.Add(successor);
        await _db.SaveChangesAsync(ct);

        return new RefreshRotationResult.Success(existing.User!, newRaw, newExpiresAt);
    }

    public async Task RevokeAsync(string rawToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return;

        var hash = HashToken(rawToken);
        var now  = _clock.GetUtcNow();

        var existing = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (existing is null || existing.RevokedAt is not null) return;

        existing.RevokedAt = now;
        await _db.SaveChangesAsync(ct);
    }

    private async Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }

    private static string GenerateRawToken()
    {
        // 256 bits of entropy, base64url-encoded for cookie/url safety.
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string HashToken(string raw)
    {
        var bytes = Encoding.UTF8.GetBytes(raw);
        var hash  = SHA256.HashData(bytes);
        return Convert.ToHexString(hash); // 64 chars, fits the 128 column easily
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> input)
    {
        var s = Convert.ToBase64String(input);
        return s.Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
