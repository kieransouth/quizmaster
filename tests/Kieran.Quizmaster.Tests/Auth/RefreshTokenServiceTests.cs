using Kieran.Quizmaster.Application.Auth;
using Kieran.Quizmaster.Domain.Entities;
using Kieran.Quizmaster.Infrastructure.Auth;
using Kieran.Quizmaster.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Kieran.Quizmaster.Tests.Auth;

public class RefreshTokenServiceTests
{
    private static User SeedUser(SqliteTestDb db)
    {
        var user = new User
        {
            Id          = Guid.NewGuid(),
            UserName    = "host@example.test",
            Email       = "host@example.test",
            DisplayName = "Host",
        };
        db.Db.Users.Add(user);
        db.Db.SaveChanges();
        return user;
    }

    private static (RefreshTokenService Sut, FakeTimeProvider Clock) BuildSut(SqliteTestDb db, TimeSpan? lifetime = null)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 4, 15, 12, 0, 0, TimeSpan.Zero));
        var opts  = Options.Create(new JwtOptions
        {
            SigningKey           = "test-signing-key-must-be-at-least-32-chars-long",
            RefreshTokenLifetime = lifetime ?? TimeSpan.FromDays(30),
        });
        return (new RefreshTokenService(db.Db, opts, clock), clock);
    }

    [Fact]
    public async Task Issue_stores_hashed_token_and_returns_raw()
    {
        using var db = new SqliteTestDb();
        var user = SeedUser(db);
        var (sut, clock) = BuildSut(db);

        var (raw, expiresAt) = await sut.IssueAsync(user, default);

        raw.ShouldNotBeNullOrWhiteSpace();
        expiresAt.ShouldBe(clock.GetUtcNow().AddDays(30));

        var stored = await db.Db.RefreshTokens.SingleAsync();
        stored.UserId.ShouldBe(user.Id);
        stored.TokenHash.ShouldNotBe(raw); // hash != raw
        stored.RevokedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Rotate_happy_path_chains_old_to_new()
    {
        using var db = new SqliteTestDb();
        var user = SeedUser(db);
        var (sut, clock) = BuildSut(db);
        var (rawOld, _) = await sut.IssueAsync(user, default);

        clock.Advance(TimeSpan.FromMinutes(5));
        var result = await sut.RotateAsync(rawOld, default);

        var success = result.ShouldBeOfType<RefreshRotationResult.Success>();
        success.User.Id.ShouldBe(user.Id);
        success.NewRefreshToken.ShouldNotBe(rawOld);

        var tokens = await db.Db.RefreshTokens.ToListAsync();
        tokens.Count.ShouldBe(2);
        var oldRow = tokens.Single(t => t.RevokedAt != null);
        var newRow = tokens.Single(t => t.RevokedAt == null);
        oldRow.RevokedAt.ShouldBe(clock.GetUtcNow());
        oldRow.ReplacedByTokenId.ShouldBe(newRow.Id);
    }

    [Fact]
    public async Task Rotate_unknown_token_returns_invalid()
    {
        using var db = new SqliteTestDb();
        var (sut, _) = BuildSut(db);

        var result = await sut.RotateAsync("totally-not-a-real-token", default);

        result.ShouldBeOfType<RefreshRotationResult.InvalidOrExpired>();
    }

    [Fact]
    public async Task Rotate_expired_token_returns_invalid()
    {
        using var db = new SqliteTestDb();
        var user = SeedUser(db);
        var (sut, clock) = BuildSut(db, TimeSpan.FromMinutes(5));
        var (raw, _) = await sut.IssueAsync(user, default);

        clock.Advance(TimeSpan.FromHours(1));
        var result = await sut.RotateAsync(raw, default);

        result.ShouldBeOfType<RefreshRotationResult.InvalidOrExpired>();
    }

    [Fact]
    public async Task Rotate_reuse_of_revoked_token_revokes_whole_family()
    {
        using var db = new SqliteTestDb();
        var user = SeedUser(db);
        var (sut, clock) = BuildSut(db);

        // Legitimate flow: issue → rotate → rotate again. Now we have:
        //   t0 (revoked, replaced by t1)
        //   t1 (revoked, replaced by t2)
        //   t2 (active)
        var (raw0, _) = await sut.IssueAsync(user, default);
        clock.Advance(TimeSpan.FromMinutes(1));
        var s1 = (RefreshRotationResult.Success)await sut.RotateAsync(raw0, default);
        clock.Advance(TimeSpan.FromMinutes(1));
        _ = (RefreshRotationResult.Success)await sut.RotateAsync(s1.NewRefreshToken, default);

        // Now an attacker (or confused client) presents the already-revoked t0 again.
        clock.Advance(TimeSpan.FromMinutes(1));
        var result = await sut.RotateAsync(raw0, default);

        result.ShouldBeOfType<RefreshRotationResult.ReuseDetected>();

        // Family revocation: every token for that user is now revoked.
        var live = await db.Db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .CountAsync();
        live.ShouldBe(0);
    }

    [Fact]
    public async Task Revoke_marks_existing_token_revoked()
    {
        using var db = new SqliteTestDb();
        var user = SeedUser(db);
        var (sut, clock) = BuildSut(db);
        var (raw, _) = await sut.IssueAsync(user, default);

        clock.Advance(TimeSpan.FromMinutes(2));
        await sut.RevokeAsync(raw, default);

        var stored = await db.Db.RefreshTokens.SingleAsync();
        stored.RevokedAt.ShouldBe(clock.GetUtcNow());
    }

    [Fact]
    public async Task Revoke_unknown_token_is_a_noop()
    {
        using var db = new SqliteTestDb();
        var (sut, _) = BuildSut(db);

        await Should.NotThrowAsync(() => sut.RevokeAsync("nope", default));
        (await db.Db.RefreshTokens.CountAsync()).ShouldBe(0);
    }
}
