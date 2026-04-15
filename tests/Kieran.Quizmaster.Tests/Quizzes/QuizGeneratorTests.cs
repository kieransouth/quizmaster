using Kieran.Quizmaster.Application.Ai;
using Kieran.Quizmaster.Application.Quizzes;
using Kieran.Quizmaster.Application.Quizzes.Dtos;
using Kieran.Quizmaster.Domain.Enumerations;
using Kieran.Quizmaster.Infrastructure.Ai.Quizzes;
using Microsoft.Extensions.AI;
using NSubstitute;
using Shouldly;

namespace Kieran.Quizmaster.Tests.Quizzes;

public class QuizGeneratorTests
{
    private static (QuizGenerator Sut, IChatClient Client, IFactChecker FactChecker) Build()
    {
        var factory = Substitute.For<IAiChatClientFactory>();
        var client  = Substitute.For<IChatClient>();
        var checker = Substitute.For<IFactChecker>();
        factory.Create(Arg.Any<AiProviderKind>(), Arg.Any<string>()).Returns(client);
        return (new QuizGenerator(factory, checker), client, checker);
    }

    private static void StubChatResponse(IChatClient client, string json) =>
        client.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
              .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, json)));

    private static GenerateQuizRequest StarWars(int count = 2, bool factCheck = false) => new(
        Title: "Star Wars night",
        Topics: [new TopicRequest("Star Wars", count)],
        MultipleChoiceFraction: 0.5,
        RunFactCheck: factCheck,
        Provider: "Ollama",
        Model: "llama3.2:1b");

    private static async Task<List<GenerationEvent>> Drain(IAsyncEnumerable<GenerationEvent> stream)
    {
        var list = new List<GenerationEvent>();
        await foreach (var e in stream) list.Add(e);
        return list;
    }

    [Fact]
    public async Task Happy_path_emits_status_questions_then_done()
    {
        var (sut, client, _) = Build();
        StubChatResponse(client, """
        {
          "questions": [
            { "topic": "Star Wars", "text": "Who is Luke's father?", "type": "FreeText", "correctAnswer": "Darth Vader", "options": null, "explanation": null },
            { "topic": "Star Wars", "text": "What planet is Tatooine?", "type": "MultipleChoice", "correctAnswer": "Desert", "options": ["Forest","Desert","Ocean","Ice"], "explanation": null }
          ]
        }
        """);

        var events = await Drain(sut.GenerateAsync(StarWars(2), default));

        events.OfType<GenerationEvent.Status>().Select(s => s.Stage)
              .ShouldContain("generating");
        events.OfType<GenerationEvent.Question>().Count().ShouldBe(2);
        events.OfType<GenerationEvent.Question>().Select(q => q.Item.Order).ShouldBe([0, 1]);
        events.Last().ShouldBeOfType<GenerationEvent.Done>();
    }

    [Fact]
    public async Task Unknown_provider_yields_non_retryable_error()
    {
        var (sut, _, _) = Build();
        var req = StarWars() with { Provider = "DefinitelyNotAProvider" };

        var events = await Drain(sut.GenerateAsync(req, default));

        var err = events.Last().ShouldBeOfType<GenerationEvent.Error>();
        err.Retryable.ShouldBeFalse();
        err.Message.ShouldContain("DefinitelyNotAProvider");
        events.OfType<GenerationEvent.Question>().ShouldBeEmpty();
    }

    [Fact]
    public async Task Factory_failure_yields_non_retryable_error()
    {
        var factory = Substitute.For<IAiChatClientFactory>();
        var checker = Substitute.For<IFactChecker>();
        factory.Create(Arg.Any<AiProviderKind>(), Arg.Any<string>())
               .Returns(_ => throw new InvalidOperationException("Model 'foo' not in allowlist"));
        var sut = new QuizGenerator(factory, checker);

        var events = await Drain(sut.GenerateAsync(StarWars(), default));

        var err = events.Last().ShouldBeOfType<GenerationEvent.Error>();
        err.Retryable.ShouldBeFalse();
        err.Message.ShouldContain("not in allowlist");
    }

    [Fact]
    public async Task Bad_JSON_eventually_yields_retryable_error_after_one_retry()
    {
        var (sut, client, _) = Build();
        // Both attempts return garbage.
        StubChatResponse(client, "this is not json at all");

        var events = await Drain(sut.GenerateAsync(StarWars(), default));

        var err = events.Last().ShouldBeOfType<GenerationEvent.Error>();
        err.Retryable.ShouldBeTrue();
        // We made the call twice (one + one retry).
        await client.Received(2).GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Partial_count_emits_warning()
    {
        var (sut, client, _) = Build();
        // Asked for 3 Star Wars, model returns 1.
        StubChatResponse(client, """
        {"questions":[{"topic":"Star Wars","text":"Q","type":"FreeText","correctAnswer":"A","options":null,"explanation":null}]}
        """);
        var req = StarWars(3);

        var events = await Drain(sut.GenerateAsync(req, default));

        var warning = events.OfType<GenerationEvent.Warning>().Single();
        warning.Message.ShouldContain("Star Wars");
        warning.Message.ShouldContain("3");
        warning.Message.ShouldContain("got 1");
    }

    [Fact]
    public async Task Fact_check_off_does_not_call_checker()
    {
        var (sut, client, checker) = Build();
        StubChatResponse(client, """
        {"questions":[{"topic":"Star Wars","text":"Q","type":"FreeText","correctAnswer":"A","options":null,"explanation":null}]}
        """);

        await Drain(sut.GenerateAsync(StarWars(1, factCheck: false), default));

        await checker.DidNotReceive().CheckAsync(
            Arg.Any<DraftQuiz>(), Arg.Any<AiProviderKind>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fact_check_on_invokes_checker_and_uses_its_result()
    {
        var (sut, client, checker) = Build();
        StubChatResponse(client, """
        {"questions":[{"topic":"Star Wars","text":"Q","type":"FreeText","correctAnswer":"A","options":null,"explanation":null}]}
        """);
        // Checker says "this question is wrong, here's why".
        checker.CheckAsync(Arg.Any<DraftQuiz>(), Arg.Any<AiProviderKind>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
               .Returns(call =>
               {
                   var draft = call.Arg<DraftQuiz>();
                   var flagged = draft.Questions[0] with { FactCheckFlagged = true, FactCheckNote = "wrong actually" };
                   return draft with { Questions = [flagged] };
               });

        var events = await Drain(sut.GenerateAsync(StarWars(1, factCheck: true), default));

        events.OfType<GenerationEvent.Status>().Select(s => s.Stage)
              .ShouldContain("fact-checking");
        var question = events.OfType<GenerationEvent.Question>().Single().Item;
        question.FactCheckFlagged.ShouldBeTrue();
        question.FactCheckNote.ShouldBe("wrong actually");
    }

    [Fact]
    public async Task Done_event_carries_full_draft_with_questions()
    {
        var (sut, client, _) = Build();
        StubChatResponse(client, """
        {"questions":[{"topic":"Star Wars","text":"Q","type":"FreeText","correctAnswer":"A","options":null,"explanation":null}]}
        """);

        var events = await Drain(sut.GenerateAsync(StarWars(1), default));

        var done = events.OfType<GenerationEvent.Done>().Single();
        done.Quiz.Title.ShouldBe("Star Wars night");
        done.Quiz.Source.ShouldBe("Generated");
        done.Quiz.ProviderUsed.ShouldBe("Ollama");
        done.Quiz.ModelUsed.ShouldBe("llama3.2:1b");
        done.Quiz.Questions.Count.ShouldBe(1);
        done.Quiz.SourceText.ShouldBeNull();
    }

    [Fact]
    public async Task Json_wrapped_in_markdown_is_extracted()
    {
        var (sut, client, _) = Build();
        // Some models stubbornly wrap JSON despite instructions.
        StubChatResponse(client, """
        Sure, here you go:
        ```json
        {"questions":[{"topic":"Star Wars","text":"Q","type":"FreeText","correctAnswer":"A","options":null,"explanation":null}]}
        ```
        """);

        var events = await Drain(sut.GenerateAsync(StarWars(1), default));

        events.OfType<GenerationEvent.Question>().Count().ShouldBe(1);
        events.Last().ShouldBeOfType<GenerationEvent.Done>();
    }
}
