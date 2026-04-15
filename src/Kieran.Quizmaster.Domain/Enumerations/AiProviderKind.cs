using Ardalis.SmartEnum;

namespace Kieran.Quizmaster.Domain.Enumerations;

public sealed class AiProviderKind : SmartEnum<AiProviderKind>
{
    public static readonly AiProviderKind Ollama    = new(nameof(Ollama), 1);
    public static readonly AiProviderKind OpenAI    = new(nameof(OpenAI), 2);
    public static readonly AiProviderKind Anthropic = new(nameof(Anthropic), 3);

    private AiProviderKind(string name, int value) : base(name, value) { }
}
