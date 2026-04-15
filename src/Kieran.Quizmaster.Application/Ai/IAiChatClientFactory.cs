using Kieran.Quizmaster.Application.Ai.Dtos;
using Kieran.Quizmaster.Domain.Enumerations;
using Microsoft.Extensions.AI;

namespace Kieran.Quizmaster.Application.Ai;

public interface IAiChatClientFactory
{
    /// <summary>
    /// Builds an IChatClient for the given provider+model. Throws
    /// <see cref="InvalidOperationException"/> when the provider isn't
    /// configured or the model isn't on its allowlist.
    /// </summary>
    IChatClient Create(AiProviderKind provider, string model);

    /// <summary>
    /// Returns the providers + per-provider model allowlists from config,
    /// suitable for populating UI dropdowns. Never includes secrets.
    /// </summary>
    AiProvidersResponse GetAvailableProviders();
}
