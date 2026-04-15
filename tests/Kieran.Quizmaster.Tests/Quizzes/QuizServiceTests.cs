using Kieran.Quizmaster.Application.Quizzes.Dtos;
using Kieran.Quizmaster.Domain.Entities;
using Kieran.Quizmaster.Infrastructure.Quizzes;
using Kieran.Quizmaster.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Kieran.Quizmaster.Tests.Quizzes;

public class QuizServiceTests
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

    private static (QuizService Sut, FakeTimeProvider Clock) BuildSut(SqliteTestDb db)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 4, 15, 12, 0, 0, TimeSpan.Zero));
        return (new QuizService(db.Db, clock), clock);
    }

    private static DraftQuiz GeneratedDraft(string title = "Star Wars night") => new(
        Title:        title,
        Source:       "Generated",
        ProviderUsed: "Ollama",
        ModelUsed:    "llama3.2:1b",
        Topics:       [new TopicRequest("Star Wars", 2)],
        SourceText:   null,
        Questions: [
            new DraftQuestion("Star Wars", "Q1", "FreeText", "A1", null, null, 0, false, null),
            new DraftQuestion("Star Wars", "Q2", "MultipleChoice", "B", ["A","B","C","D"], "because", 1, false, null),
        ]);

    private static DraftQuiz ImportedDraft() => new(
        Title:        "Imported pub quiz",
        Source:       "Imported",
        ProviderUsed: "OpenAI",
        ModelUsed:    "gpt-4o",
        Topics:       [],
        SourceText:   "1. Q? - A",
        Questions: [
            new DraftQuestion("", "Q?", "FreeText", "A", null, null, 0, false, null),
        ]);

    [Fact]
    public async Task Save_persists_quiz_with_topics_and_questions()
    {
        using var db = new SqliteTestDb();
        var user = SeedUser(db);
        var (sut, clock) = BuildSut(db);

        var id = await sut.SaveAsync(GeneratedDraft(), user.Id, default);

        var saved = await db.Db.Quizzes
            .Include(q => q.Topics)
            .Include(q => q.Questions)
            .SingleAsync(q => q.Id == id);
        saved.Title.ShouldBe("Star Wars night");
        saved.Source.Name.ShouldBe("Generated");
        saved.CreatedByUserId.ShouldBe(user.Id);
        saved.CreatedAt.ShouldBe(clock.GetUtcNow());
        saved.Topics.Count.ShouldBe(1);
        saved.Topics.Single().Name.ShouldBe("Star Wars");
        saved.Topics.Single().RequestedCount.ShouldBe(2);
        saved.Questions.Count.ShouldBe(2);
        saved.Questions.OrderBy(q => q.Order).Select(q => q.Order).ShouldBe([0, 1]);
        var mc = saved.Questions.Single(q => q.Type.Name == "MultipleChoice");
        mc.OptionsJson.ShouldNotBeNull();
        mc.OptionsJson.ShouldContain("A");
    }

    [Fact]
    public async Task Save_imported_has_no_topics_and_keeps_source_text()
    {
        using var db = new SqliteTestDb();
        var user = SeedUser(db);
        var (sut, _) = BuildSut(db);

        var id = await sut.SaveAsync(ImportedDraft(), user.Id, default);

        var saved = await db.Db.Quizzes.Include(q => q.Topics).SingleAsync(q => q.Id == id);
        saved.Source.Name.ShouldBe("Imported");
        saved.Topics.ShouldBeEmpty();
        saved.SourceText.ShouldBe("1. Q? - A");
    }

    [Fact]
    public async Task ListMine_only_returns_my_quizzes_newest_first()
    {
        using var db = new SqliteTestDb();
        var alice = SeedUser(db, "-alice");
        var bob   = SeedUser(db, "-bob");
        var (sut, clock) = BuildSut(db);

        await sut.SaveAsync(GeneratedDraft("Alice 1"), alice.Id, default);
        clock.Advance(TimeSpan.FromMinutes(1));
        await sut.SaveAsync(GeneratedDraft("Bob 1"), bob.Id, default);
        clock.Advance(TimeSpan.FromMinutes(1));
        await sut.SaveAsync(GeneratedDraft("Alice 2"), alice.Id, default);

        var list = await sut.ListMineAsync(alice.Id, default);

        list.Select(q => q.Title).ShouldBe(["Alice 2", "Alice 1"]);
        list.ShouldNotContain(q => q.Title == "Bob 1");
        list[0].QuestionCount.ShouldBe(2);
    }

    [Fact]
    public async Task GetById_returns_null_for_someone_elses_quiz()
    {
        using var db = new SqliteTestDb();
        var alice = SeedUser(db, "-alice");
        var bob   = SeedUser(db, "-bob");
        var (sut, _) = BuildSut(db);
        var id = await sut.SaveAsync(GeneratedDraft(), alice.Id, default);

        var asBob = await sut.GetByIdAsync(id, bob.Id, default);

        asBob.ShouldBeNull();
    }

    [Fact]
    public async Task GetById_returns_full_detail_when_owned()
    {
        using var db = new SqliteTestDb();
        var user = SeedUser(db);
        var (sut, _) = BuildSut(db);
        var id = await sut.SaveAsync(GeneratedDraft(), user.Id, default);

        var detail = await sut.GetByIdAsync(id, user.Id, default);

        detail.ShouldNotBeNull();
        detail!.Id.ShouldBe(id);
        detail.Questions.Count.ShouldBe(2);
        detail.Questions.Select(q => q.Order).ShouldBe([0, 1]);
        detail.Topics.Single().Name.ShouldBe("Star Wars");
    }

    [Fact]
    public async Task Update_changes_title_edits_questions_and_deletes_missing_ones()
    {
        using var db = new SqliteTestDb();
        var user = SeedUser(db);
        var (sut, _) = BuildSut(db);
        var id = await sut.SaveAsync(GeneratedDraft(), user.Id, default);
        var detail = (await sut.GetByIdAsync(id, user.Id, default))!;
        var keep = detail.Questions[0];

        // Keep one question (renamed), drop the other.
        var ok = await sut.UpdateAsync(id, user.Id, new UpdateQuizRequest(
            Title: "Renamed",
            Questions: [
                new UpdateQuestionRequest(keep.Id, "Renamed text", "Renamed answer", null, "new note", 0),
            ]), default);

        ok.ShouldBeTrue();
        var after = (await sut.GetByIdAsync(id, user.Id, default))!;
        after.Title.ShouldBe("Renamed");
        after.Questions.Count.ShouldBe(1);
        after.Questions[0].Text.ShouldBe("Renamed text");
        after.Questions[0].CorrectAnswer.ShouldBe("Renamed answer");
        after.Questions[0].Explanation.ShouldBe("new note");
    }

    [Fact]
    public async Task Update_returns_false_when_caller_does_not_own_quiz()
    {
        using var db = new SqliteTestDb();
        var alice = SeedUser(db, "-alice");
        var bob   = SeedUser(db, "-bob");
        var (sut, _) = BuildSut(db);
        var id = await sut.SaveAsync(GeneratedDraft(), alice.Id, default);

        var ok = await sut.UpdateAsync(id, bob.Id, new UpdateQuizRequest("Hijacked", []), default);

        ok.ShouldBeFalse();
        var stillThere = await db.Db.Quizzes.SingleAsync(q => q.Id == id);
        stillThere.Title.ShouldBe("Star Wars night");
    }

    [Fact]
    public async Task Delete_removes_owned_quiz()
    {
        using var db = new SqliteTestDb();
        var user = SeedUser(db);
        var (sut, _) = BuildSut(db);
        var id = await sut.SaveAsync(GeneratedDraft(), user.Id, default);

        var ok = await sut.DeleteAsync(id, user.Id, default);

        ok.ShouldBeTrue();
        (await db.Db.Quizzes.AnyAsync(q => q.Id == id)).ShouldBeFalse();
    }

    [Fact]
    public async Task Delete_returns_false_for_someone_elses_quiz()
    {
        using var db = new SqliteTestDb();
        var alice = SeedUser(db, "-alice");
        var bob   = SeedUser(db, "-bob");
        var (sut, _) = BuildSut(db);
        var id = await sut.SaveAsync(GeneratedDraft(), alice.Id, default);

        var ok = await sut.DeleteAsync(id, bob.Id, default);

        ok.ShouldBeFalse();
        (await db.Db.Quizzes.AnyAsync(q => q.Id == id)).ShouldBeTrue();
    }
}
