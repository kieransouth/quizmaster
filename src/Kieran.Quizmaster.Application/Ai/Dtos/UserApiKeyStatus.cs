namespace Kieran.Quizmaster.Application.Ai.Dtos;

/// <summary>
/// Per-provider status for the Settings UI. <see cref="Masked"/> shows the
/// last few characters of the stored key (or null when unset). The full key
/// is never round-tripped back to the client.
/// </summary>
public record UserApiKeyStatus(
    string  Provider,
    bool    HasKey,
    string? Masked);
