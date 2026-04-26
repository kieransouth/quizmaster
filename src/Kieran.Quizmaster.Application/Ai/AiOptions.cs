namespace Kieran.Quizmaster.Application.Ai;

/// <summary>
/// Bound from the "Ai" config section. Provider+model selection is
/// per-request from the UI, but the *set* of allowed providers and the
/// per-provider model allowlist live here so the UI populates from a
/// trusted source and we can reject hostile inputs at the factory.
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>Provider name (matches AiProviderKind.Name) used when none specified.</summary>
    public string DefaultProvider { get; init; } = "Ollama";

    /// <summary>Model name used when none specified.</summary>
    public string DefaultModel { get; init; } = string.Empty;

    /// <summary>Keyed by provider name (e.g. "Ollama", "OpenAI", "Anthropic").</summary>
    public Dictionary<string, AiProviderConfig> Providers { get; init; } = new();
}

public sealed class AiProviderConfig
{
    /// <summary>
    /// For self-hosted providers (e.g. the Ollama URL). API keys for cloud
    /// providers are per-user and live in <c>UserApiKeys</c>.
    /// </summary>
    public string? BaseUrl { get; init; }

    /// <summary>
    /// Per-provider on/off switch. Defaults to true. Lets the server admin
    /// hide a provider entirely from the UI — primarily so the Ollama
    /// daemon shared by the live demo can be disabled there while
    /// remaining available to self-hosted instances.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Allowlist of model identifiers usable with this provider.</summary>
    public List<string> Models { get; init; } = new();
}
