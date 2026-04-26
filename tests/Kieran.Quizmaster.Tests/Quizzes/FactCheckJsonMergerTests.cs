using Kieran.Quizmaster.Application.Quizzes.Dtos;
using Kieran.Quizmaster.Infrastructure.Ai.Quizzes;
using Shouldly;

namespace Kieran.Quizmaster.Tests.Quizzes;

public class FactCheckJsonMergerTests
{
    private static IReadOnlyList<DraftQuestion> Questions(int n) =>
        Enumerable.Range(0, n)
            .Select(i => new DraftQuestion(
                Topic:            "T",
                Text:             $"Q{i}",
                Type:             "FreeText",
                CorrectAnswer:    $"A{i}",
                Options:          null,
                Explanation:      null,
                Order:            i,
                FactCheckFlagged: false,
                FactCheckNote:    null))
            .ToList();

    [Fact]
    public void Applies_flags_by_index_on_clean_json()
    {
        var qs = Questions(3);
        var json = """
        {"checks":[
          {"questionIndex":0,"factuallyCorrect":true, "note":null},
          {"questionIndex":1,"factuallyCorrect":false,"note":"that's wrong"},
          {"questionIndex":2,"factuallyCorrect":true, "note":null}
        ]}
        """;

        var merged = FactCheckJsonMerger.Apply(qs, json);

        merged[0].FactCheckFlagged.ShouldBeFalse();
        merged[1].FactCheckFlagged.ShouldBeTrue();
        merged[1].FactCheckNote.ShouldBe("that's wrong");
        merged[2].FactCheckFlagged.ShouldBeFalse();
    }

    [Fact]
    public void Missing_indices_leave_their_questions_unflagged()
    {
        var qs = Questions(3);
        var json = """{"checks":[{"questionIndex":1,"factuallyCorrect":false,"note":"only this one"}]}""";

        var merged = FactCheckJsonMerger.Apply(qs, json);

        merged[0].FactCheckFlagged.ShouldBeFalse();
        merged[1].FactCheckFlagged.ShouldBeTrue();
        merged[2].FactCheckFlagged.ShouldBeFalse();
    }

    [Fact]
    public void Out_of_range_indices_are_silently_skipped()
    {
        var qs = Questions(2);
        // Index 99 doesn't exist; we shouldn't blow up the whole merge.
        var json = """
        {"checks":[
          {"questionIndex":99,"factuallyCorrect":false,"note":"nonsense"},
          {"questionIndex":0, "factuallyCorrect":false,"note":"real"}
        ]}
        """;

        var merged = FactCheckJsonMerger.Apply(qs, json);

        merged[0].FactCheckFlagged.ShouldBeTrue();
        merged[0].FactCheckNote.ShouldBe("real");
    }

    [Fact]
    public void Markdown_fenced_json_is_unwrapped_before_parsing()
    {
        var qs = Questions(1);
        var json = """
        Sure, here you go:

        ```json
        {"checks":[{"questionIndex":0,"factuallyCorrect":false,"note":"nope"}]}
        ```
        """;

        var merged = FactCheckJsonMerger.Apply(qs, json);

        merged[0].FactCheckFlagged.ShouldBeTrue();
    }

    [Fact]
    public void Empty_input_throws_with_useful_message()
    {
        var qs = Questions(1);
        var ex = Should.Throw<InvalidOperationException>(() => FactCheckJsonMerger.Apply(qs, ""));
        ex.Message.ShouldContain("empty");
    }

    [Fact]
    public void Malformed_json_throws_with_useful_message()
    {
        var qs = Questions(1);
        var ex = Should.Throw<InvalidOperationException>(
            () => FactCheckJsonMerger.Apply(qs, "{{{not json"));
        ex.Message.ShouldContain("parse");
    }

    [Fact]
    public void Missing_checks_array_throws_with_useful_message()
    {
        var qs = Questions(1);
        var ex = Should.Throw<InvalidOperationException>(
            () => FactCheckJsonMerger.Apply(qs, """{"something":"else"}"""));
        ex.Message.ShouldContain("checks");
    }

    [Fact]
    public void Flag_clears_note_when_factually_correct_again()
    {
        // Start with a question that's already flagged from a prior run.
        var qs = new List<DraftQuestion>
        {
            new(
                Topic: "T", Text: "Q", Type: "FreeText", CorrectAnswer: "A",
                Options: null, Explanation: null, Order: 0,
                FactCheckFlagged: true,
                FactCheckNote:    "stale note from a previous run"),
        };
        var json = """{"checks":[{"questionIndex":0,"factuallyCorrect":true,"note":null}]}""";

        var merged = FactCheckJsonMerger.Apply(qs, json);

        merged[0].FactCheckFlagged.ShouldBeFalse();
        merged[0].FactCheckNote.ShouldBeNull();
    }
}
