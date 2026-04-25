using Anthropic.SDK;
using Kieran.Quizmaster.Application.Ai;
using Kieran.Quizmaster.Application.Ai.Dtos;
using Kieran.Quizmaster.Domain.Enumerations;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OpenAI;

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
            2 /* OpenAI */    => CreateOpenAI(cfg, model),
            3 /* Anthropic */ => CreateAnthropic(cfg, model),
            _ => throw new InvalidOperationException($"Unknown provider value: {provider.Value}")
        };
    }

    private static IChatClient CreateOpenAI(AiProviderConfig cfg, string model)
    {
        if (string.IsNullOrWhiteSpace(cfg.ApiKey))
            throw new InvalidOperationException(
                "OpenAI provider requires an API key. Set Ai__Providers__OpenAI__ApiKey.");

        return new OpenAIClient(cfg.ApiKey)
            .GetChatClient(model)
            .AsIChatClient();
    }

    private static IChatClient CreateAnthropic(AiProviderConfig cfg, string model)
    {
        if (string.IsNullOrWhiteSpace(cfg.ApiKey))
            throw new InvalidOperationException(
                "Anthropic provider requires an API key. Set Ai__Providers__Anthropic__ApiKey.");

        // MessagesEndpoint implements IChatClient but reads ModelId from
        // per-call ChatOptions. Wrap with a ChatClientBuilder that injects
        // the configured model when callers don't supply one.
        IChatClient inner = new AnthropicClient(cfg.ApiKey).Messages;
        return new ChatClientBuilder(inner)
            .ConfigureOptions(o => o.ModelId ??= model)
            .Build();
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
