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

    /// <summary>Build an IAsyncEnumerable&lt;ChatResponseUpdate&gt; from text chunks.</summary>
    private static async IAsyncEnumerable<ChatResponseUpdate> AsStream(params string[] chunks)
    {
        foreach (var chunk in chunks)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, chunk);
            await Task.Yield();
        }
    }

    /// <summary>Make GetStreamingResponseAsync yield the given chunks (rebuilds per call).</summary>
    private static void StubStream(IChatClient client, params string[] chunks) =>
        client.GetStreamingResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
              .Returns(_ => AsStream(chunks));

    /// <summary>Convenience: stub with a single chunk containing a complete JSON document.</summary>
    private static void StubFullJson(IChatClient client, string json) => StubStream(client, json);

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
        StubFullJson(client, """
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
    public async Task Streams_questions_progressively_as_chunks_arrive()
    {
        // Split the JSON across multiple chunks at arbitrary points to prove
        // the parser handles incremental token boundaries correctly.
        var (sut, client, _) = Build();
        StubStream(client,
            "{\"questions\":[{\"topic\":\"Star Wars\",\"text\":\"Q1\",\"type\":",
            "\"FreeText\",\"correctAnswer\":\"A1\",\"options\":null,\"explanation\":null}",
            ",{\"topic\":\"Star Wars\",\"text\":\"Q2\",\"type\":\"FreeText\",",
            "\"correctAnswer\":\"A2\",\"options\":null,\"explanation\":null}]}");

        var events = await Drain(sut.GenerateAsync(StarWars(2), default));

        events.OfType<GenerationEvent.Question>().Count().ShouldBe(2);
        events.OfType<GenerationEvent.Question>().Select(q => q.Item.Text).ShouldBe(["Q1", "Q2"]);
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
    public async Task Garbage_stream_with_no_questions_yields_retryable_error()
    {
        var (sut, client, _) = Build();
        StubStream(client, "this is not json at all");

        var events = await Drain(sut.GenerateAsync(StarWars(), default));

        var err = events.Last().ShouldBeOfType<GenerationEvent.Error>();
        err.Retryable.ShouldBeTrue();
        err.Message.ShouldContain("no questions");
    }

    [Fact]
    public async Task Partial_count_emits_warning()
    {
        var (sut, client, _) = Build();
        // Asked for 3 Star Wars, model returns 1.
        StubFullJson(client, """
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
        StubFullJson(client, """
        {"questions":[{"topic":"Star Wars","text":"Q","type":"FreeText","correctAnswer":"A","options":null,"explanation":null}]}
        """);

        await Drain(sut.GenerateAsync(StarWars(1, factCheck: false), default));

        await checker.DidNotReceive().CheckAsync(
            Arg.Any<DraftQuiz>(), Arg.Any<AiProviderKind>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fact_check_on_re_emits_flagged_question()
    {
        var (sut, client, checker) = Build();
        StubFullJson(client, """
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

        // Two question events: one streamed live (unflagged) + one re-emit after fact-check (flagged).
        var questionEvents = events.OfType<GenerationEvent.Question>().Select(q => q.Item).ToList();
        questionEvents.Count.ShouldBe(2);
        questionEvents[0].FactCheckFlagged.ShouldBeFalse();
        questionEvents[1].FactCheckFlagged.ShouldBeTrue();
        questionEvents[1].FactCheckNote.ShouldBe("wrong actually");

        // Done event reflects the final (flagged) state.
        var done = events.OfType<GenerationEvent.Done>().Single();
        done.Quiz.Questions[0].FactCheckFlagged.ShouldBeTrue();
    }

    [Fact]
    public async Task Done_event_carries_full_draft_with_questions()
    {
        var (sut, client, _) = Build();
        StubFullJson(client, """
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
        // Some models stubbornly wrap JSON despite instructions. The streaming
        // parser ignores everything before the first '['.
        StubStream(client,
            """Sure, here you go:""", "\n```json\n",
            """{"questions":[{"topic":"Star Wars","text":"Q","type":"FreeText","correctAnswer":"A","options":null,"explanation":null}]}""",
            "\n```");

        var events = await Drain(sut.GenerateAsync(StarWars(1), default));

        events.OfType<GenerationEvent.Question>().Count().ShouldBe(1);
        events.Last().ShouldBeOfType<GenerationEvent.Done>();
    }
}
