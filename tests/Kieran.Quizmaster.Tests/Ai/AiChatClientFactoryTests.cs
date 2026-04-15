using Kieran.Quizmaster.Application.Ai;
using Kieran.Quizmaster.Domain.Enumerations;
using Kieran.Quizmaster.Infrastructure.Ai;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Kieran.Quizmaster.Tests.Ai;

public class AiChatClientFactoryTests
{
    private static AiChatClientFactory Build(AiOptions opts) =>
        new(Options.Create(opts));

    private static AiOptions DefaultOpts() => new()
    {
        DefaultProvider = "Ollama",
        DefaultModel    = "llama3.2:1b",
        Providers = new()
        {
            ["Ollama"] = new AiProviderConfig
            {
                BaseUrl = "http://localhost:11434",
                Models  = ["llama3.2:1b", "qwen2.5:7b"],
            },
            ["OpenAI"] = new AiProviderConfig
            {
                Models = ["gpt-5"],
            },
        },
    };

    [Fact]
    public void Create_returns_chat_client_for_configured_ollama_model()
    {
        var sut = Build(DefaultOpts());

        var client = sut.Create(AiProviderKind.Ollama, "llama3.2:1b");

        client.ShouldBeAssignableTo<IChatClient>();
    }

    [Fact]
    public void Create_throws_for_unconfigured_provider()
    {
        // Anthropic isn't in the Providers dictionary in DefaultOpts.
        var sut = Build(DefaultOpts());

        var ex = Should.Throw<InvalidOperationException>(
            () => sut.Create(AiProviderKind.Anthropic, "claude-opus-4-6"));

        ex.Message.ShouldContain("Anthropic");
        ex.Message.ShouldContain("not configured");
    }

    [Fact]
    public void Create_throws_when_model_is_not_in_allowlist()
    {
        var sut = Build(DefaultOpts());

        var ex = Should.Throw<InvalidOperationException>(
            () => sut.Create(AiProviderKind.Ollama, "some-arbitrary-model"));

        ex.Message.ShouldContain("not in the allowlist");
        ex.Message.ShouldContain("Ollama");
    }

    [Fact]
    public void Create_for_OpenAI_throws_when_no_api_key()
    {
        // DefaultOpts has OpenAI in the providers list but with no ApiKey.
        var sut = Build(DefaultOpts());

        var ex = Should.Throw<InvalidOperationException>(
            () => sut.Create(AiProviderKind.OpenAI, "gpt-5"));

        ex.Message.ShouldContain("OpenAI");
        ex.Message.ShouldContain("API key");
    }

    [Fact]
    public void Create_for_OpenAI_returns_chat_client_when_key_provided()
    {
        var opts = DefaultOpts();
        opts.Providers["OpenAI"] = new AiProviderConfig
        {
            ApiKey = "sk-test-not-real",
            Models = ["gpt-5"],
        };
        var sut = Build(opts);

        var client = sut.Create(AiProviderKind.OpenAI, "gpt-5");

        client.ShouldBeAssignableTo<IChatClient>();
    }

    [Fact]
    public void GetAvailableProviders_returns_configured_providers_and_models()
    {
        var sut = Build(DefaultOpts());

        var resp = sut.GetAvailableProviders();

        resp.DefaultProvider.ShouldBe("Ollama");
        resp.DefaultModel.ShouldBe("llama3.2:1b");
        resp.Providers.Count.ShouldBe(2);

        var ollama = resp.Providers.Single(p => p.Provider == "Ollama");
        ollama.Models.ShouldBe(["llama3.2:1b", "qwen2.5:7b"]);

        var openai = resp.Providers.Single(p => p.Provider == "OpenAI");
        openai.Models.ShouldBe(["gpt-5"]);
    }

    [Fact]
    public void GetAvailableProviders_does_not_leak_secrets()
    {
        var opts = DefaultOpts();
        opts.Providers["OpenAI"] = new AiProviderConfig
        {
            ApiKey = "sk-secret-xyz",
            Models = ["gpt-5"],
        };
        var sut = Build(opts);

        var resp = sut.GetAvailableProviders();

        // Round-trip through serialization to be paranoid about hidden fields.
        var json = System.Text.Json.JsonSerializer.Serialize(resp);
        json.ShouldNotContain("sk-secret-xyz");
        json.ShouldNotContain("ApiKey", Case.Insensitive);
    }
}
