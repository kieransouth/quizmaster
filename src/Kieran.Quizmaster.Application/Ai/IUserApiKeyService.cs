using Kieran.Quizmaster.Application.Ai.Dtos;

namespace Kieran.Quizmaster.Application.Ai;

/// <summary>
/// Manages per-user AI provider API keys. Implementations encrypt at rest;
/// the service boundary trades in plaintext (for round-tripping into the
/// chat-client factory) and a masked preview (for the Settings UI).
/// </summary>
public interface IUserApiKeyService
{
    /// <summary>Returns the decrypted key for (user, provider), or null if not set.</summary>
    Task<string?> GetKeyAsync(Guid userId, string provider, CancellationToken ct = default);

    /// <summary>Set or replace the key. Plaintext in, encrypted at rest.</summary>
    Task SetKeyAsync(Guid userId, string provider, string apiKey, CancellationToken ct = default);

    /// <summary>Remove the key for (user, provider). No-op if it's not set.</summary>
    Task RemoveKeyAsync(Guid userId, string provider, CancellationToken ct = default);

    /// <summary>
    /// Lists all known providers (from <c>AiOptions</c>) with the user's set/unset
    /// status and a masked preview of the key when set. Never returns plaintext.
    /// </summary>
    Task<IReadOnlyList<UserApiKeyStatus>> ListAsync(Guid userId, CancellationToken ct = default);
}
