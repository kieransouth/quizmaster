namespace Kieran.Quizmaster.Application.Ai.Dtos;

public sealed record AiProviderInfo(string Provider, IReadOnlyList<string> Models);

public sealed record AiProvidersResponse(
    string DefaultProvider,
    string DefaultModel,
    IReadOnlyList<AiProviderInfo> Providers);
