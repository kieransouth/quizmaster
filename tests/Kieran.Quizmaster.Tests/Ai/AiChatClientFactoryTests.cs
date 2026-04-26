using Kieran.Quizmaster.Application.Ai;
using Kieran.Quizmaster.Application.Ai.Dtos;
using Kieran.Quizmaster.Domain.Enumerations;
using Kieran.Quizmaster.Infrastructure.Ai;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Kieran.Quizmaster.Tests.Ai;

public class AiChatClientFactoryTests
{
    private static readonly Guid TestUser = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static AiChatClientFactory Build(AiOptions opts, IUserApiKeyService? keys = null) =>
        new(Options.Create(opts), keys ?? Substitute.For<IUserApiKeyService>());

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
            ["Anthropic"] = new AiProviderConfig
            {
                Models = ["claude-opus-4-6"],
            },
        },
    };

    [Fact]
    public async Task Create_returns_chat_client_for_configured_ollama_model()
    {
        var sut = Build(DefaultOpts());

        var client = await sut.CreateAsync(TestUser, AiProviderKind.Ollama, "llama3.2:1b");

        client.ShouldBeAssignableTo<IChatClient>();
    }

    [Fact]
    public async Task Create_throws_for_unconfigured_provider()
    {
        var opts = DefaultOpts();
        opts.Providers.Remove("Anthropic");
        var sut = Build(opts);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.CreateAsync(TestUser, AiProviderKind.Anthropic, "claude-opus-4-6"));

        ex.Message.ShouldContain("Anthropic");
        ex.Message.ShouldContain("not configured");
    }

    [Fact]
    public async Task Create_throws_when_provider_is_disabled()
    {
        var opts = DefaultOpts();
        opts.Providers["Ollama"] = new AiProviderConfig
        {
            BaseUrl = opts.Providers["Ollama"].BaseUrl,
            Enabled = false,
            Models  = opts.Providers["Ollama"].Models,
        };
        var sut = Build(opts);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.CreateAsync(TestUser, AiProviderKind.Ollama, "llama3.2:1b"));

        ex.Message.ShouldContain("Ollama");
        ex.Message.ShouldContain("disabled");
    }

    [Fact]
    public async Task Create_throws_when_model_is_not_in_allowlist()
    {
        var sut = Build(DefaultOpts());

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.CreateAsync(TestUser, AiProviderKind.Ollama, "some-arbitrary-model"));

        ex.Message.ShouldContain("not in the allowlist");
        ex.Message.ShouldContain("Ollama");
    }

    [Fact]
    public async Task Create_for_OpenAI_throws_when_user_has_no_key()
    {
        var keys = Substitute.For<IUserApiKeyService>();
        keys.GetKeyAsync(TestUser, "OpenAI", Arg.Any<CancellationToken>())
            .Returns((string?)null);
        var sut = Build(DefaultOpts(), keys);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.CreateAsync(TestUser, AiProviderKind.OpenAI, "gpt-5"));

        ex.Message.ShouldContain("OpenAI");
        ex.Message.ShouldContain("API key");
    }

    [Fact]
    public async Task Create_for_OpenAI_returns_chat_client_when_user_has_key()
    {
        var keys = Substitute.For<IUserApiKeyService>();
        keys.GetKeyAsync(TestUser, "OpenAI", Arg.Any<CancellationToken>())
            .Returns("sk-test-not-real");
        var sut = Build(DefaultOpts(), keys);

        var client = await sut.CreateAsync(TestUser, AiProviderKind.OpenAI, "gpt-5");

        client.ShouldBeAssignableTo<IChatClient>();
    }

    [Fact]
    public async Task Create_for_Anthropic_throws_when_user_has_no_key()
    {
        var keys = Substitute.For<IUserApiKeyService>();
        keys.GetKeyAsync(TestUser, "Anthropic", Arg.Any<CancellationToken>())
            .Returns((string?)null);
        var sut = Build(DefaultOpts(), keys);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.CreateAsync(TestUser, AiProviderKind.Anthropic, "claude-opus-4-6"));

        ex.Message.ShouldContain("Anthropic");
        ex.Message.ShouldContain("API key");
    }

    [Fact]
    public async Task Create_for_Anthropic_returns_chat_client_when_user_has_key()
    {
        var keys = Substitute.For<IUserApiKeyService>();
        keys.GetKeyAsync(TestUser, "Anthropic", Arg.Any<CancellationToken>())
            .Returns("sk-ant-test-not-real");
        var sut = Build(DefaultOpts(), keys);

        var client = await sut.CreateAsync(TestUser, AiProviderKind.Anthropic, "claude-opus-4-6");

        client.ShouldBeAssignableTo<IChatClient>();
    }

    [Fact]
    public async Task GetAvailableProviders_includes_ollama_when_enabled()
    {
        var keys = Substitute.For<IUserApiKeyService>();
        keys.ListAsync(TestUser, Arg.Any<CancellationToken>())
            .Returns(new List<UserApiKeyStatus>
            {
                new("Ollama",    false, null),
                new("OpenAI",    false, null),
                new("Anthropic", false, null),
            });
        var sut = Build(DefaultOpts(), keys);

        var resp = await sut.GetAvailableProvidersAsync(TestUser);

        resp.Providers.Select(p => p.Provider).ShouldBe(["Ollama"]);
        resp.DefaultProvider.ShouldBe("Ollama");
    }

    [Fact]
    public async Task GetAvailableProviders_includes_cloud_provider_only_when_user_has_key()
    {
        var keys = Substitute.For<IUserApiKeyService>();
        keys.ListAsync(TestUser, Arg.Any<CancellationToken>())
            .Returns(new List<UserApiKeyStatus>
            {
                new("Ollama",    false, null),
                new("OpenAI",    true,  "•••••5"),
                new("Anthropic", false, null),
            });
        var sut = Build(DefaultOpts(), keys);

        var resp = await sut.GetAvailableProvidersAsync(TestUser);

        resp.Providers.Select(p => p.Provider).Order().ShouldBe(["Ollama", "OpenAI"]);
    }

    [Fact]
    public async Task GetAvailableProviders_excludes_disabled_provider()
    {
        var opts = DefaultOpts();
        opts.Providers["Ollama"] = new AiProviderConfig
        {
            BaseUrl = opts.Providers["Ollama"].BaseUrl,
            Enabled = false,
            Models  = opts.Providers["Ollama"].Models,
        };
        var keys = Substitute.For<IUserApiKeyService>();
        keys.ListAsync(TestUser, Arg.Any<CancellationToken>())
            .Returns(new List<UserApiKeyStatus>
            {
                new("OpenAI",    true,  "•••••5"),
                new("Anthropic", false, null),
            });
        var sut = Build(opts, keys);

        var resp = await sut.GetAvailableProvidersAsync(TestUser);

        resp.Providers.Select(p => p.Provider).ShouldBe(["OpenAI"]);
        resp.Providers.ShouldNotContain(p => p.Provider == "Ollama");
    }
}
