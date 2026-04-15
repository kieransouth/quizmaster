using Kieran.Quizmaster.Application.Ai;
using Kieran.Quizmaster.Application.Ai.Dtos;
using Kieran.Quizmaster.Domain.Enumerations;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OllamaSharp;

namespace Kieran.Quizmaster.Infrastructure.Ai;

public sealed class AiChatClientFactory(IOptions<AiOptions> options) : IAiChatClientFactory
{
    private readonly AiOptions _options = options.Value;

    public IChatClient Create(AiProviderKind provider, string model)
    {
        if (!_options.Providers.TryGetValue(provider.Name, out var cfg))
            throw new InvalidOperationException(
                $"AI provider '{provider.Name}' is not configured. Available: " +
                $"[{string.Join(", ", _options.Providers.Keys)}]");

        if (!cfg.Models.Contains(model))
            throw new InvalidOperationException(
                $"Model '{model}' is not in the allowlist for provider '{provider.Name}'. " +
                $"Allowed: [{string.Join(", ", cfg.Models)}]");

        // Switch on the SmartEnum's value so the compiler reminds us when we
        // add a new provider kind.
        return provider.Value switch
        {
            1 /* Ollama */    => CreateOllama(cfg, model),
            2 /* OpenAI */    => throw new NotImplementedException(
                "OpenAI adapter not yet wired (planned for Phase 5)."),
            3 /* Anthropic */ => throw new NotImplementedException(
                "Anthropic adapter not yet wired (planned for Phase 5)."),
            _ => throw new InvalidOperationException($"Unknown provider value: {provider.Value}")
        };
    }

    public AiProvidersResponse GetAvailableProviders()
    {
        var providers = _options.Providers
            .Select(kvp => new AiProviderInfo(kvp.Key, kvp.Value.Models))
            .ToList();

        return new AiProvidersResponse(
            _options.DefaultProvider,
            _options.DefaultModel,
            providers);
    }

    private static IChatClient CreateOllama(AiProviderConfig cfg, string model)
    {
        var baseUrl = cfg.BaseUrl ?? "http://localhost:11434";
        // OllamaApiClient implements IChatClient.
        return new OllamaApiClient(new Uri(baseUrl), model);
    }
}
