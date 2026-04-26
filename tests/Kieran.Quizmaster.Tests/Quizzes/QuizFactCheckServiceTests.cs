using Kieran.Quizmaster.Application.Quizzes;
using Kieran.Quizmaster.Application.Quizzes.Dtos;
using Kieran.Quizmaster.Domain.Entities;
using Kieran.Quizmaster.Domain.Enumerations;
using Kieran.Quizmaster.Infrastructure.Quizzes;
using Kieran.Quizmaster.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace Kieran.Quizmaster.Tests.Quizzes;

public class QuizFactCheckServiceTests
{
    private static User SeedUser(SqliteTestDb db, string suffix = "")
    {
        var user = new User
        {
            Id          = Guid.NewGuid(),
            UserName    = $"host{suffix}@example.test",
            Email       = $"host{suffix}@example.test",
            DisplayName = $"Host{suffix}",
        };
        db.Db.Users.Add(user);
        db.Db.SaveChanges();
        return user;
    }

    private static DraftQuiz GeneratedDraft() => new(
        Title:        "Star Wars night",
        Source:       "Generated",
        ProviderUsed: "Ollama",
        ModelUsed:    "llama3.2:1b",
        Topics:       [new TopicRequest("Star Wars", 2)],
        SourceText:   null,
        Questions: [
            new DraftQuestion("Star Wars", "Q1", "FreeText", "A1", null, null, 0, false, null),
            new DraftQuestion("Star Wars", "Q2", "MultipleChoice", "B", ["A", "B", "C", "D"], "because", 1, false, null),
        ]);

    private static (QuizFactCheckService Sut, IFactChecker FactChecker, QuizService QuizService)
        BuildSut(SqliteTestDb db)
    {
        var clock        = new FakeTimeProvider(new DateTimeOffset(2026, 4, 26, 12, 0, 0, TimeSpan.Zero));
        var quizService  = new QuizService(db.Db, clock);
        var factChecker  = Substitute.For<IFactChecker>();
        var sut          = new QuizFactCheckService(factChecker, quizService, db.Db);
        return (sut, factChecker, quizService);
    }

    private static IReadOnlyList<DraftQuestion> Drafts(int n) =>
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
    public async Task ApplyAiAsync_returns_what_FactChecker_returns()
    {
        using var db = new SqliteTestDb();
        var (sut, checker, _) = BuildSut(db);

        var input = Drafts(2);
        // Pretend the model flags question 1.
        checker.CheckAsync(
                Arg.Any<Guid>(), Arg.Any<DraftQuiz>(), Arg.Any<AiProviderKind>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var draft = call.Arg<DraftQuiz>();
                return draft with
                {
                    Questions =
                    [
                        draft.Questions[0],
                        draft.Questions[1] with { FactCheckFlagged = true, FactCheckNote = "wrong" },
                    ],
                };
            });

        var merged = await sut.ApplyAiAsync(
            Guid.NewGuid(), input, AiProviderKind.OpenAI, "gpt-5", default);

        merged[0].FactCheckFlagged.ShouldBeFalse();
        merged[1].FactCheckFlagged.ShouldBeTrue();
        merged[1].FactCheckNote.ShouldBe("wrong");
    }

    [Fact]
    public void ApplyJson_delegates_to_merger()
    {
        using var db = new SqliteTestDb();
        var (sut, _, _) = BuildSut(db);

        var merged = sut.ApplyJson(
            Drafts(2),
            """{"checks":[{"questionIndex":0,"factuallyCorrect":false,"note":"nope"}]}""");

        merged[0].FactCheckFlagged.ShouldBeTrue();
        merged[0].FactCheckNote.ShouldBe("nope");
        merged[1].FactCheckFlagged.ShouldBeFalse();
    }

    [Fact]
    public async Task ApplyJsonToSavedAsync_persists_only_flag_fields()
    {
        using var db = new SqliteTestDb();
        var user = SeedUser(db);
        var (sut, _, quizService) = BuildSut(db);
        var quizId = await quizService.SaveAsync(GeneratedDraft(), user.Id, default);

        var json = """{"checks":[{"questionIndex":1,"factuallyCorrect":false,"note":"flagged"}]}""";

        var updated = await sut.ApplyJsonToSavedAsync(quizId, user.Id, json, default);

        updated.ShouldNotBeNull();
        updated!.Questions[0].FactCheckFlagged.ShouldBe(false);
        updated.Questions[1].FactCheckFlagged.ShouldBe(true);
        updated.Questions[1].FactCheckNote.ShouldBe("flagged");
        // Ensure non-flag fields untouched.
        updated.Questions[0].Text.ShouldBe("Q1");
        updated.Questions[1].Text.ShouldBe("Q2");
        updated.Questions[1].Options.ShouldBe(["A", "B", "C", "D"]);
        updated.Questions[1].Order.ShouldBe(1);
    }

    [Fact]
    public async Task ApplyJsonToSavedAsync_returns_null_for_other_users_quiz()
    {
        using var db = new SqliteTestDb();
        var owner = SeedUser(db, "owner");
        var other = SeedUser(db, "other");
        var (sut, _, quizService) = BuildSut(db);
        var quizId = await quizService.SaveAsync(GeneratedDraft(), owner.Id, default);

        var result = await sut.ApplyJsonToSavedAsync(
            quizId, other.Id,
            """{"checks":[{"questionIndex":0,"factuallyCorrect":true,"note":null}]}""",
            default);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ApplyJsonToSavedAsync_throws_on_malformed_json()
    {
        using var db = new SqliteTestDb();
        var user = SeedUser(db);
        var (sut, _, quizService) = BuildSut(db);
        var quizId = await quizService.SaveAsync(GeneratedDraft(), user.Id, default);

        await Should.ThrowAsync<InvalidOperationException>(
            () => sut.ApplyJsonToSavedAsync(quizId, user.Id, "{not json", default));
    }

    [Fact]
    public async Task ApplyAiToSavedAsync_persists_flag_changes()
    {
        using var db = new SqliteTestDb();
        var user = SeedUser(db);
        var (sut, checker, quizService) = BuildSut(db);
        var quizId = await quizService.SaveAsync(GeneratedDraft(), user.Id, default);

        // FactChecker flags question 0.
        checker.CheckAsync(
                Arg.Any<Guid>(), Arg.Any<DraftQuiz>(), Arg.Any<AiProviderKind>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var draft = call.Arg<DraftQuiz>();
                return draft with
                {
                    Questions =
                    [
                        draft.Questions[0] with { FactCheckFlagged = true, FactCheckNote = "ai flagged" },
                        draft.Questions[1],
                    ],
                };
            });

        var updated = await sut.ApplyAiToSavedAsync(
            quizId, user.Id, AiProviderKind.OpenAI, "gpt-5", default);

        updated.ShouldNotBeNull();
        updated!.Questions[0].FactCheckFlagged.ShouldBeTrue();
        updated.Questions[0].FactCheckNote.ShouldBe("ai flagged");

        // Round-trip via the DB to confirm persistence (not just the in-memory DTO).
        var reloaded = await quizService.GetByIdAsync(quizId, user.Id, default);
        reloaded.ShouldNotBeNull();
        reloaded!.Questions[0].FactCheckFlagged.ShouldBeTrue();
    }
}
