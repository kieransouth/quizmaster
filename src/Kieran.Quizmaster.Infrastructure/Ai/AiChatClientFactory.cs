using Anthropic.SDK;
using Kieran.Quizmaster.Application.Ai;
using Kieran.Quizmaster.Application.Ai.Dtos;
using Kieran.Quizmaster.Domain.Enumerations;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OpenAI;

namespace Kieran.Quizmaster.Infrastructure.Ai;

public sealed class AiChatClientFactory(
    IOptions<AiOptions>  options,
    IUserApiKeyService   keys) : IAiChatClientFactory
{
    private readonly AiOptions _options = options.Value;

    public async Task<IChatClient> CreateAsync(
        Guid userId, AiProviderKind provider, string model, CancellationToken ct = default)
    {
        if (!_options.Providers.TryGetValue(provider.Name, out var cfg))
            throw new InvalidOperationException(
                $"AI provider '{provider.Name}' is not configured. Available: " +
                $"[{string.Join(", ", _options.Providers.Keys)}]");

        if (!cfg.Enabled)
            throw new InvalidOperationException(
                $"AI provider '{provider.Name}' is disabled on this server.");

        if (!cfg.Models.Contains(model))
            throw new InvalidOperationException(
                $"Model '{model}' is not in the allowlist for provider '{provider.Name}'. " +
                $"Allowed: [{string.Join(", ", cfg.Models)}]");

        // Switch on the SmartEnum's value so the compiler reminds us when we
        // add a new provider kind.
        return provider.Value switch
        {
            1 /* Ollama */    => CreateOllama(cfg, model),
            2 /* OpenAI */    => CreateOpenAI(model, await RequireUserKeyAsync(userId, provider.Name, ct)),
            3 /* Anthropic */ => CreateAnthropic(model, await RequireUserKeyAsync(userId, provider.Name, ct)),
            _ => throw new InvalidOperationException($"Unknown provider value: {provider.Value}")
        };
    }

    public async Task<AiProvidersResponse> GetAvailableProvidersAsync(
        Guid userId, CancellationToken ct = default)
    {
        var statuses = await keys.ListAsync(userId, ct);
        var keyByProvider = statuses.ToDictionary(s => s.Provider, s => s.HasKey);

        var providers = _options.Providers
            .Where(kvp => kvp.Value.Enabled)
            .Where(kvp => RequiresUserKey(kvp.Key) ? keyByProvider.GetValueOrDefault(kvp.Key) : true)
            .Select(kvp => new AiProviderInfo(kvp.Key, kvp.Value.Models))
            .ToList();

        // Defaults only matter when they're still in the available list.
        var defaultProvider = providers.Any(p => p.Provider == _options.DefaultProvider)
            ? _options.DefaultProvider
            : providers.FirstOrDefault()?.Provider ?? string.Empty;

        var defaultModel = providers
            .FirstOrDefault(p => p.Provider == defaultProvider)
            ?.Models.Contains(_options.DefaultModel) == true
                ? _options.DefaultModel
                : providers.FirstOrDefault(p => p.Provider == defaultProvider)?.Models.FirstOrDefault() ?? string.Empty;

        return new AiProvidersResponse(defaultProvider, defaultModel, providers);
    }

    private async Task<string> RequireUserKeyAsync(Guid userId, string provider, CancellationToken ct)
    {
        var key = await keys.GetKeyAsync(userId, provider, ct);
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException(
                $"No {provider} API key on file for the current user. Add one in Settings before generating with this provider.");
        return key;
    }

    private static bool RequiresUserKey(string providerName) =>
        providerName != AiProviderKind.Ollama.Name;

    private static IChatClient CreateOpenAI(string model, string apiKey) =>
        new OpenAIClient(apiKey).GetChatClient(model).AsIChatClient();

    private static IChatClient CreateAnthropic(string model, string apiKey)
    {
        // MessagesEndpoint implements IChatClient but reads ModelId from
        // per-call ChatOptions. Wrap with a ChatClientBuilder that injects
        // the configured model when callers don't supply one.
        IChatClient inner = new AnthropicClient(apiKey).Messages;
        return new ChatClientBuilder(inner)
            .ConfigureOptions(o => o.ModelId ??= model)
            .Build();
    }

    private static IChatClient CreateOllama(AiProviderConfig cfg, string model)
    {
        var baseUrl = cfg.BaseUrl ?? "http://localhost:11434";
        // OllamaApiClient implements IChatClient.
        return new OllamaApiClient(new Uri(baseUrl), model);
    }
}
