using Kieran.Quizmaster.Application.Quizzes;
using Kieran.Quizmaster.Application.Quizzes.Dtos;
using Kieran.Quizmaster.Infrastructure.Ai.Quizzes;
using Shouldly;

namespace Kieran.Quizmaster.Tests.Quizzes;

public class QuizJsonImporterTests
{
    private static QuizJsonImporter Build() => new();

    private static ImportFromJsonRequest Req(string json, string title = "Pasted") => new(
        Title:      title,
        Topics:     [],
        SourceJson: json);

    private static async Task<List<GenerationEvent>> Drain(IAsyncEnumerable<GenerationEvent> stream)
    {
        var list = new List<GenerationEvent>();
        await foreach (var e in stream) list.Add(e);
        return list;
    }

    [Fact]
    public async Task Valid_JSON_yields_status_questions_then_done()
    {
        var sut = Build();
        var json = """
        {"questions":[
          {"topic":"Star Wars","text":"Q1","type":"FreeText","correctAnswer":"A1","options":null,"explanation":null},
          {"topic":"Star Wars","text":"Q2","type":"MultipleChoice","correctAnswer":"B","options":["A","B","C","D"],"explanation":null}
        ]}
        """;

        var events = await Drain(sut.ImportFromJsonAsync(Req(json), default));

        events.OfType<GenerationEvent.Status>().Select(s => s.Stage).ShouldContain("parsing");
        events.OfType<GenerationEvent.Question>().Count().ShouldBe(2);
        events.OfType<GenerationEvent.Question>().Select(q => q.Item.Order).ShouldBe([0, 1]);
        events.Last().ShouldBeOfType<GenerationEvent.Done>();
    }

    [Fact]
    public async Task Markdown_fenced_JSON_is_extracted()
    {
        var sut = Build();
        var json = """
        Sure, here's your quiz:
        ```json
        {"questions":[{"topic":"X","text":"Q","type":"FreeText","correctAnswer":"A","options":null,"explanation":null}]}
        ```
        """;

        var events = await Drain(sut.ImportFromJsonAsync(Req(json), default));

        events.OfType<GenerationEvent.Question>().Count().ShouldBe(1);
        events.Last().ShouldBeOfType<GenerationEvent.Done>();
    }

    [Fact]
    public async Task Empty_input_yields_clear_error()
    {
        var sut = Build();

        var events = await Drain(sut.ImportFromJsonAsync(Req(""), default));

        var err = events.Last().ShouldBeOfType<GenerationEvent.Error>();
        err.Message.ShouldContain("paste");
        err.Retryable.ShouldBeFalse();
    }

    [Fact]
    public async Task Garbage_input_yields_parse_error()
    {
        var sut = Build();

        var events = await Drain(sut.ImportFromJsonAsync(Req("definitely not json"), default));

        var err = events.Last().ShouldBeOfType<GenerationEvent.Error>();
        err.Message.ShouldContain("parse");
    }

    [Fact]
    public async Task Empty_questions_array_yields_error()
    {
        var sut = Build();

        var events = await Drain(sut.ImportFromJsonAsync(Req("""{"questions":[]}"""), default));

        var err = events.Last().ShouldBeOfType<GenerationEvent.Error>();
        err.Message.ShouldContain("no questions");
    }

    [Fact]
    public async Task Topic_count_warning_when_fewer_returned_than_requested()
    {
        var sut = Build();
        var json = """
        {"questions":[{"topic":"Star Wars","text":"Q","type":"FreeText","correctAnswer":"A","options":null,"explanation":null}]}
        """;
        var req = new ImportFromJsonRequest(
            Title: "Test",
            Topics: [new TopicRequest("Star Wars", 3)],
            SourceJson: json);

        var events = await Drain(sut.ImportFromJsonAsync(req, default));

        var warning = events.OfType<GenerationEvent.Warning>().Single();
        warning.Message.ShouldContain("3");
        warning.Message.ShouldContain("got 1");
    }

    [Fact]
    public async Task Done_event_carries_full_draft()
    {
        var sut = Build();
        var json = """
        {"questions":[{"topic":"Star Wars","text":"Q","type":"FreeText","correctAnswer":"A","options":null,"explanation":null}]}
        """;

        var events = await Drain(sut.ImportFromJsonAsync(
            new ImportFromJsonRequest("Pasted", [], json), default));

        var done = events.OfType<GenerationEvent.Done>().Single();
        done.Quiz.Title.ShouldBe("Pasted");
        done.Quiz.Source.ShouldBe("Imported");
        done.Quiz.ProviderUsed.ShouldBe("Manual");
        done.Quiz.ModelUsed.ShouldBe("Manual");
        done.Quiz.Questions.Count.ShouldBe(1);
        done.Quiz.SourceText.ShouldBe(json);
    }
}
