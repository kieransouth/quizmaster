using System.Text.Json;
using Kieran.Quizmaster.Application.Sessions;
using Kieran.Quizmaster.Application.Sessions.Dtos;
using Kieran.Quizmaster.Domain.Entities;
using Kieran.Quizmaster.Domain.Enumerations;
using Kieran.Quizmaster.Infrastructure.Sessions;
using Kieran.Quizmaster.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Kieran.Quizmaster.Tests.Sessions;

public class SessionServiceTests
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

    /// <summary>Seeds a user + a 2-question quiz (1 MC, 1 FreeText) and returns ids.</summary>
    private static (User Owner, Quiz Quiz, Guid McId, Guid FreeId) SeedQuiz(SqliteTestDb db)
    {
        var owner = SeedUser(db);
        var mcId   = Guid.NewGuid();
        var freeId = Guid.NewGuid();
        var quiz = new Quiz
        {
            Id              = Guid.NewGuid(),
            Title           = "Test quiz",
            Source          = QuizSource.Generated,
            ProviderUsed    = "Ollama",
            ModelUsed       = "llama3.2:1b",
            CreatedByUserId = owner.Id,
            CreatedAt       = DateTimeOffset.UtcNow,
            Questions =
            [
                new Question {
                    Id            = mcId,
                    Text          = "MC?",
                    Type          = QuestionType.MultipleChoice,
                    CorrectAnswer = "B",
                    OptionsJson   = JsonSerializer.Serialize(new[] { "A", "B", "C", "D" }),
                    Order         = 0,
                },
                new Question {
                    Id            = freeId,
                    Text          = "FreeText?",
                    Type          = QuestionType.FreeText,
                    CorrectAnswer = "Forty Two",
                    Order         = 1,
                },
            ],
        };
        db.Db.Quizzes.Add(quiz);
        db.Db.SaveChanges();
        return (owner, quiz, mcId, freeId);
    }

    private static (SessionService Sut, FakeTimeProvider Clock) Build(SqliteTestDb db)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 4, 15, 12, 0, 0, TimeSpan.Zero));
        return (new SessionService(db.Db, clock), clock);
    }

    [Fact]
    public async Task Start_creates_session_with_share_token_and_blank_answers()
    {
        using var db = new SqliteTestDb();
        var (owner, quiz, _, _) = SeedQuiz(db);
        var (sut, clock) = Build(db);

        var session = await sut.StartAsync(quiz.Id, owner.Id, default);

        session.ShouldNotBeNull();
        session!.Status.ShouldBe("InProgress");
        session.StartedAt.ShouldBe(clock.GetUtcNow());
        session.PublicShareToken.ShouldNotBeNullOrWhiteSpace();
        session.Questions.Count.ShouldBe(2);
        session.Questions.ShouldAllBe(q => q.Answer.AnswerText == "");
        session.Questions.ShouldAllBe(q => q.CorrectAnswer == null); // hidden while InProgress
    }

    [Fact]
    public async Task Start_returns_null_for_someone_elses_quiz()
    {
        using var db = new SqliteTestDb();
        var (_, quiz, _, _) = SeedQuiz(db);
        var stranger = SeedUser(db, "-stranger");
        var (sut, _) = Build(db);

        var session = await sut.StartAsync(quiz.Id, stranger.Id, default);

        session.ShouldBeNull();
    }

    [Fact]
    public async Task RecordAnswer_upserts_while_InProgress()
    {
        using var db = new SqliteTestDb();
        var (owner, quiz, mcId, _) = SeedQuiz(db);
        var (sut, _) = Build(db);
        var session = await sut.StartAsync(quiz.Id, owner.Id, default);

        var result = await sut.RecordAnswerAsync(session!.Id, mcId, owner.Id, new RecordAnswerRequest("B"), default);

        var ok = result.ShouldBeOfType<SessionResult.Ok>();
        ok.Session.Questions.Single(q => q.Id == mcId).Answer.AnswerText.ShouldBe("B");
    }

    [Fact]
    public async Task RecordAnswer_rejects_after_reveal()
    {
        using var db = new SqliteTestDb();
        var (owner, quiz, mcId, _) = SeedQuiz(db);
        var (sut, _) = Build(db);
        var session = await sut.StartAsync(quiz.Id, owner.Id, default);
        await sut.RevealAsync(session!.Id, owner.Id, default);

        var result = await sut.RecordAnswerAsync(session.Id, mcId, owner.Id, new RecordAnswerRequest("changed"), default);

        result.ShouldBeOfType<SessionResult.InvalidState>();
    }

    [Fact]
    public async Task Reveal_auto_grades_MC_correct_and_incorrect()
    {
        using var db = new SqliteTestDb();
        var (owner, quiz, mcId, freeId) = SeedQuiz(db);
        var (sut, _) = Build(db);
        var session = await sut.StartAsync(quiz.Id, owner.Id, default);
        await sut.RecordAnswerAsync(session!.Id, mcId,   owner.Id, new RecordAnswerRequest("B"), default);
        await sut.RecordAnswerAsync(session.Id,  freeId, owner.Id, new RecordAnswerRequest("guess"), default);

        var result = await sut.RevealAsync(session.Id, owner.Id, default);

        var ok = result.ShouldBeOfType<SessionResult.Ok>();
        ok.Session.Status.ShouldBe("AwaitingReveal");
        var mc = ok.Session.Questions.Single(q => q.Id == mcId);
        mc.Answer.IsCorrect.ShouldBe(true);
        mc.Answer.PointsAwarded.ShouldBe(1m);
        // Free-text remains ungraded
        var free = ok.Session.Questions.Single(q => q.Id == freeId);
        free.Answer.IsCorrect.ShouldBeNull();
        // Correct answers now visible
        mc.CorrectAnswer.ShouldBe("B");
    }

    [Fact]
    public async Task Reveal_auto_grades_MC_with_wrong_answer_as_zero()
    {
        using var db = new SqliteTestDb();
        var (owner, quiz, mcId, _) = SeedQuiz(db);
        var (sut, _) = Build(db);
        var session = await sut.StartAsync(quiz.Id, owner.Id, default);
        await sut.RecordAnswerAsync(session!.Id, mcId, owner.Id, new RecordAnswerRequest("A"), default);

        var ok = (SessionResult.Ok)await sut.RevealAsync(session.Id, owner.Id, default);

        var mc = ok.Session.Questions.Single(q => q.Id == mcId);
        mc.Answer.IsCorrect.ShouldBe(false);
        mc.Answer.PointsAwarded.ShouldBe(0m);
    }

    [Fact]
    public async Task GradeAnswer_only_works_after_reveal()
    {
        using var db = new SqliteTestDb();
        var (owner, quiz, _, freeId) = SeedQuiz(db);
        var (sut, _) = Build(db);
        var session = await sut.StartAsync(quiz.Id, owner.Id, default);

        // Before reveal: rejected.
        var before = await sut.GradeAnswerAsync(session!.Id, freeId, owner.Id,
            new GradeAnswerRequest(true, 1m, null), default);
        before.ShouldBeOfType<SessionResult.InvalidState>();

        // After reveal: works, partial credit clamps to [0,1].
        await sut.RevealAsync(session.Id, owner.Id, default);
        var after = await sut.GradeAnswerAsync(session.Id, freeId, owner.Id,
            new GradeAnswerRequest(true, 0.5m, "close enough"), default);
        var ok = after.ShouldBeOfType<SessionResult.Ok>();
        var graded = ok.Session.Questions.Single(q => q.Id == freeId);
        graded.Answer.IsCorrect.ShouldBe(true);
        graded.Answer.PointsAwarded.ShouldBe(0.5m);
        graded.Answer.GradingNote.ShouldBe("close enough");
    }

    [Fact]
    public async Task GradeAnswer_clamps_points_to_unit_interval()
    {
        using var db = new SqliteTestDb();
        var (owner, quiz, _, freeId) = SeedQuiz(db);
        var (sut, _) = Build(db);
        var session = await sut.StartAsync(quiz.Id, owner.Id, default);
        await sut.RevealAsync(session!.Id, owner.Id, default);

        var ok = (SessionResult.Ok)await sut.GradeAnswerAsync(session.Id, freeId, owner.Id,
            new GradeAnswerRequest(true, 7m, null), default);

        ok.Session.Questions.Single(q => q.Id == freeId).Answer.PointsAwarded.ShouldBe(1m);
    }

    [Fact]
    public async Task Complete_rejects_when_any_answer_ungraded()
    {
        using var db = new SqliteTestDb();
        var (owner, quiz, mcId, _) = SeedQuiz(db);
        var (sut, _) = Build(db);
        var session = await sut.StartAsync(quiz.Id, owner.Id, default);
        await sut.RecordAnswerAsync(session!.Id, mcId, owner.Id, new RecordAnswerRequest("B"), default);
        await sut.RevealAsync(session.Id, owner.Id, default);
        // Free-text still ungraded.

        var result = await sut.CompleteAsync(session.Id, owner.Id, default);

        result.ShouldBeOfType<SessionResult.InvalidState>();
    }

    [Fact]
    public async Task Complete_succeeds_after_all_graded_and_stamps_completed_at()
    {
        using var db = new SqliteTestDb();
        var (owner, quiz, mcId, freeId) = SeedQuiz(db);
        var (sut, clock) = Build(db);
        var session = await sut.StartAsync(quiz.Id, owner.Id, default);
        await sut.RecordAnswerAsync(session!.Id, mcId,   owner.Id, new RecordAnswerRequest("B"), default);
        await sut.RecordAnswerAsync(session.Id,  freeId, owner.Id, new RecordAnswerRequest("Forty Two"), default);
        await sut.RevealAsync(session.Id, owner.Id, default);
        await sut.GradeAnswerAsync(session.Id, freeId, owner.Id, new GradeAnswerRequest(true, 1m, null), default);

        clock.Advance(TimeSpan.FromMinutes(30));
        var result = await sut.CompleteAsync(session.Id, owner.Id, default);

        var ok = result.ShouldBeOfType<SessionResult.Ok>();
        ok.Session.Status.ShouldBe("Graded");
        ok.Session.CompletedAt.ShouldBe(clock.GetUtcNow());
        ok.Session.Score.ShouldBe(2m);
        ok.Session.MaxScore.ShouldBe(2);
    }

    [Fact]
    public async Task Get_returns_null_for_someone_elses_session()
    {
        using var db = new SqliteTestDb();
        var (owner, quiz, _, _) = SeedQuiz(db);
        var stranger = SeedUser(db, "-stranger");
        var (sut, _) = Build(db);
        var session = await sut.StartAsync(quiz.Id, owner.Id, default);

        var asStranger = await sut.GetByIdAsync(session!.Id, stranger.Id, default);

        asStranger.ShouldBeNull();
    }

    // ----- MC option shuffle (Phase post-7 product asks) -----

    /// <summary>Seeds a quiz with one MC question that has 8 options so shuffle collisions are negligible.</summary>
    private static (User Owner, Quiz Quiz, Guid McId) SeedQuizWithLargeMc(SqliteTestDb db)
    {
        var owner = SeedUser(db);
        var mcId  = Guid.NewGuid();
        var quiz = new Quiz
        {
            Id              = Guid.NewGuid(),
            Title           = "Shuffle quiz",
            Source          = QuizSource.Generated,
            ProviderUsed    = "Ollama",
            ModelUsed       = "llama3.2:1b",
            CreatedByUserId = owner.Id,
            CreatedAt       = DateTimeOffset.UtcNow,
            Questions =
            [
                new Question {
                    Id            = mcId,
                    Text          = "Pick one",
                    Type          = QuestionType.MultipleChoice,
                    CorrectAnswer = "Echo",
                    OptionsJson   = JsonSerializer.Serialize(
                        new[] { "Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot", "Golf", "Hotel" }),
                    Order         = 0,
                },
            ],
        };
        db.Db.Quizzes.Add(quiz);
        db.Db.SaveChanges();
        return (owner, quiz, mcId);
    }

    [Fact]
    public async Task MC_options_are_shuffled_in_session_view()
    {
        using var db = new SqliteTestDb();
        var (owner, quiz, mcId) = SeedQuizWithLargeMc(db);
        var (sut, _) = Build(db);
        var canonical = new[] { "Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot", "Golf", "Hotel" };

        var session = await sut.StartAsync(quiz.Id, owner.Id, default);

        var options = session!.Questions.Single(q => q.Id == mcId).Options!;
        // Same set of strings, but at least one position differs (probability
        // of identical permutation with 8 unique options is 1/40320).
        options.Count.ShouldBe(canonical.Length);
        options.OrderBy(o => o).ShouldBe(canonical.OrderBy(o => o));
        options.SequenceEqual(canonical).ShouldBeFalse(
            "options should be shuffled; matching the canonical order is statistically nearly impossible");
    }

    [Fact]
    public async Task MC_option_shuffle_is_deterministic_within_a_session()
    {
        using var db = new SqliteTestDb();
        var (owner, quiz, mcId) = SeedQuizWithLargeMc(db);
        var (sut, _) = Build(db);
        var session = await sut.StartAsync(quiz.Id, owner.Id, default);

        var first  = (await sut.GetByIdAsync(session!.Id, owner.Id, default))!
                     .Questions.Single(q => q.Id == mcId).Options!;
        var second = (await sut.GetByIdAsync(session.Id, owner.Id, default))!
                     .Questions.Single(q => q.Id == mcId).Options!;

        second.SequenceEqual(first).ShouldBeTrue();
    }

    [Fact]
    public async Task MC_option_shuffle_differs_across_sessions()
    {
        using var db = new SqliteTestDb();
        var (owner, quiz, mcId) = SeedQuizWithLargeMc(db);
        var (sut, _) = Build(db);

        var s1 = await sut.StartAsync(quiz.Id, owner.Id, default);
        var s2 = await sut.StartAsync(quiz.Id, owner.Id, default);

        var o1 = s1!.Questions.Single(q => q.Id == mcId).Options!;
        var o2 = s2!.Questions.Single(q => q.Id == mcId).Options!;

        o1.SequenceEqual(o2).ShouldBeFalse(
            "two different sessions over the same question should produce different option orders");
    }

    [Fact]
    public async Task Reveal_auto_grades_correctly_after_shuffle()
    {
        // Even when the model originally placed the correct answer at position
        // 4 (Echo), shuffling shouldn't break grading because we match by
        // text, not index.
        using var db = new SqliteTestDb();
        var (owner, quiz, mcId) = SeedQuizWithLargeMc(db);
        var (sut, _) = Build(db);
        var session = await sut.StartAsync(quiz.Id, owner.Id, default);

        await sut.RecordAnswerAsync(session!.Id, mcId, owner.Id, new RecordAnswerRequest("Echo"), default);
        var ok = (SessionResult.Ok)await sut.RevealAsync(session.Id, owner.Id, default);

        var graded = ok.Session.Questions.Single(q => q.Id == mcId);
        graded.Answer.IsCorrect.ShouldBe(true);
        graded.Answer.PointsAwarded.ShouldBe(1m);
    }

    // ----- Public share lookup (Phase 8) -----

    /// <summary>Plays a 2-question quiz to completion and returns the share token.</summary>
    private static async Task<string> PlayToCompletionAsync(
        SessionService sut, User owner, Quiz quiz, Guid mcId, Guid freeId,
        string mcAnswer = "B", string freeAnswer = "Forty Two", decimal freePoints = 1m)
    {
        var session = await sut.StartAsync(quiz.Id, owner.Id, default);
        await sut.RecordAnswerAsync(session!.Id, mcId,   owner.Id, new RecordAnswerRequest(mcAnswer),   default);
        await sut.RecordAnswerAsync(session.Id,  freeId, owner.Id, new RecordAnswerRequest(freeAnswer), default);
        await sut.RevealAsync(session.Id, owner.Id, default);
        await sut.GradeAnswerAsync(session.Id, freeId, owner.Id,
            new GradeAnswerRequest(freePoints > 0, freePoints, null), default);
        await sut.CompleteAsync(session.Id, owner.Id, default);
        return session.PublicShareToken;
    }

    [Fact]
    public async Task GetByShareToken_returns_null_for_unknown_token()
    {
        using var db = new SqliteTestDb();
        var (sut, _) = Build(db);

        var summary = await sut.GetByShareTokenAsync("definitely-not-a-real-token", default);

        summary.ShouldBeNull();
    }

    [Fact]
    public async Task GetByShareToken_returns_null_while_session_is_in_progress()
    {
        using var db = new SqliteTestDb();
        var (owner, quiz, _, _) = SeedQuiz(db);
        var (sut, _) = Build(db);
        var session = await sut.StartAsync(quiz.Id, owner.Id, default);

        var summary = await sut.GetByShareTokenAsync(session!.PublicShareToken, default);

        summary.ShouldBeNull();
    }

    [Fact]
    public async Task GetByShareToken_returns_null_while_awaiting_reveal()
    {
        using var db = new SqliteTestDb();
        var (owner, quiz, mcId, _) = SeedQuiz(db);
        var (sut, _) = Build(db);
        var session = await sut.StartAsync(quiz.Id, owner.Id, default);
        await sut.RecordAnswerAsync(session!.Id, mcId, owner.Id, new RecordAnswerRequest("B"), default);
        await sut.RevealAsync(session.Id, owner.Id, default);

        var summary = await sut.GetByShareTokenAsync(session.PublicShareToken, default);

        summary.ShouldBeNull();
    }

    [Fact]
    public async Task GetByShareToken_returns_full_summary_when_graded()
    {
        using var db = new SqliteTestDb();
        var (owner, quiz, mcId, freeId) = SeedQuiz(db);
        var (sut, _) = Build(db);
        var token = await PlayToCompletionAsync(sut, owner, quiz, mcId, freeId);

        var summary = await sut.GetByShareTokenAsync(token, default);

        summary.ShouldNotBeNull();
        summary!.QuizTitle.ShouldBe("Test quiz");
        summary.Score.ShouldBe(2m);
        summary.MaxScore.ShouldBe(2);
        summary.Questions.Count.ShouldBe(2);
        summary.Questions.Select(q => q.Order).ShouldBe([0, 1]);
        summary.Questions.Single(q => q.Type == "MultipleChoice").TeamAnswer.ShouldBe("B");
        summary.Questions.Single(q => q.Type == "MultipleChoice").CorrectAnswer.ShouldBe("B");
        summary.Questions.Single(q => q.Type == "MultipleChoice").IsCorrect.ShouldBeTrue();
    }

    [Fact]
    public async Task GetByShareToken_payload_does_not_leak_host_identity()
    {
        // Paranoid serialization check — the public DTO must not carry
        // any host-identifying field. We round-trip through System.Text.Json
        // to catch anything that snuck in via property additions.
        using var db = new SqliteTestDb();
        var (owner, quiz, mcId, freeId) = SeedQuiz(db);
        var (sut, _) = Build(db);
        var token = await PlayToCompletionAsync(sut, owner, quiz, mcId, freeId);

        var summary = await sut.GetByShareTokenAsync(token, default);

        var json = System.Text.Json.JsonSerializer.Serialize(summary);
        json.ShouldNotContain(owner.Id.ToString());
        json.ShouldNotContain(owner.Email!);
        json.ShouldNotContain(owner.DisplayName);
        json.ShouldNotContain("HostUserId", Case.Insensitive);
    }
}
