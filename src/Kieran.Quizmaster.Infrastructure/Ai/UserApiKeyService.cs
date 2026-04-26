using Kieran.Quizmaster.Application.Ai;
using Kieran.Quizmaster.Application.Ai.Dtos;
using Kieran.Quizmaster.Domain.Entities;
using Kieran.Quizmaster.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kieran.Quizmaster.Infrastructure.Ai;

public sealed class UserApiKeyService : IUserApiKeyService
{
    private const string ProtectorPurpose = "Quizmaster.UserApiKey:v1";

    private readonly ApplicationDbContext _db;
    private readonly IDataProtector       _protector;
    private readonly TimeProvider         _clock;
    private readonly AiOptions            _options;

    public UserApiKeyService(
        ApplicationDbContext       db,
        IDataProtectionProvider    dpp,
        TimeProvider               clock,
        IOptions<AiOptions>        options)
    {
        _db        = db;
        _protector = dpp.CreateProtector(ProtectorPurpose);
        _clock     = clock;
        _options   = options.Value;
    }

    public async Task<string?> GetKeyAsync(Guid userId, string provider, CancellationToken ct = default)
    {
        var row = await _db.UserApiKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.UserId == userId && k.Provider == provider, ct);

        return row is null ? null : _protector.Unprotect(row.EncryptedKey);
    }

    public async Task SetKeyAsync(Guid userId, string provider, string apiKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key must not be empty.", nameof(apiKey));

        EnsureProviderConfigured(provider);

        var encrypted = _protector.Protect(apiKey);
        var now       = _clock.GetUtcNow();

        var existing = await _db.UserApiKeys
            .FirstOrDefaultAsync(k => k.UserId == userId && k.Provider == provider, ct);

        if (existing is null)
        {
            _db.UserApiKeys.Add(new UserApiKey
            {
                Id           = Guid.NewGuid(),
                UserId       = userId,
                Provider     = provider,
                EncryptedKey = encrypted,
                CreatedAt    = now,
                UpdatedAt    = now,
            });
        }
        else
        {
            existing.EncryptedKey = encrypted;
            existing.UpdatedAt    = now;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveKeyAsync(Guid userId, string provider, CancellationToken ct = default)
    {
        var existing = await _db.UserApiKeys
            .FirstOrDefaultAsync(k => k.UserId == userId && k.Provider == provider, ct);
        if (existing is null) return;

        _db.UserApiKeys.Remove(existing);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<UserApiKeyStatus>> ListAsync(Guid userId, CancellationToken ct = default)
    {
        var rows = await _db.UserApiKeys
            .AsNoTracking()
            .Where(k => k.UserId == userId)
            .ToListAsync(ct);

        var byProvider = rows.ToDictionary(r => r.Provider, r => r.EncryptedKey);

        // Surface every provider configured server-side, whether the user has
        // a key or not — the Settings UI shows a row per provider.
        return _options.Providers
            .Where(kvp => kvp.Value.Enabled)
            .Select(kvp =>
            {
                var hasKey = byProvider.TryGetValue(kvp.Key, out var encrypted);
                string? masked = null;
                if (hasKey)
                {
                    var plaintext = _protector.Unprotect(encrypted!);
                    masked = Mask(plaintext);
                }
                return new UserApiKeyStatus(kvp.Key, hasKey, masked);
            })
            .ToList();
    }

    private void EnsureProviderConfigured(string provider)
    {
        if (!_options.Providers.TryGetValue(provider, out var cfg) || !cfg.Enabled)
            throw new InvalidOperationException(
                $"Provider '{provider}' is not configured or is disabled on this server.");
    }

    /// <summary>Show the last 4 chars; everything else as •.</summary>
    private static string Mask(string plaintext) =>
        plaintext.Length <= 4
            ? new string('•', plaintext.Length)
            : new string('•', plaintext.Length - 4) + plaintext[^4..];
}
