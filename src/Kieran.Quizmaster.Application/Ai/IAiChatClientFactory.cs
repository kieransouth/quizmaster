using Kieran.Quizmaster.Application.Ai.Dtos;
using Kieran.Quizmaster.Domain.Enumerations;
using Microsoft.Extensions.AI;

namespace Kieran.Quizmaster.Application.Ai;

public interface IAiChatClientFactory
{
    /// <summary>
    /// Builds an <see cref="IChatClient"/> for the given user + provider + model.
    /// Looks up the user's saved API key for cloud providers; throws
    /// <see cref="InvalidOperationException"/> when:
    /// <list type="bullet">
    /// <item>the provider isn't configured or is disabled,</item>
    /// <item>the model isn't on the provider's allowlist,</item>
    /// <item>a cloud provider is requested but the user has no saved key.</item>
    /// </list>
    /// Ollama is a server-shared provider — it does not consult per-user keys.
    /// </summary>
    Task<IChatClient> CreateAsync(
        Guid userId, AiProviderKind provider, string model, CancellationToken ct = default);

    /// <summary>
    /// Returns the providers + per-provider model allowlists the user can
    /// actually use right now. Filters out:
    /// <list type="bullet">
    /// <item>providers disabled in server config,</item>
    /// <item>cloud providers (OpenAI/Anthropic) where the user hasn't set a key.</item>
    /// </list>
    /// </summary>
    Task<AiProvidersResponse> GetAvailableProvidersAsync(Guid userId, CancellationToken ct = default);
}
