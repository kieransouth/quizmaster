using System.Security.Cryptography;
using System.Text.Json;
using Kieran.Quizmaster.Application.Sessions;
using Kieran.Quizmaster.Application.Sessions.Dtos;
using Kieran.Quizmaster.Domain.Entities;
using Kieran.Quizmaster.Domain.Enumerations;
using Kieran.Quizmaster.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kieran.Quizmaster.Infrastructure.Sessions;

public sealed class SessionService(ApplicationDbContext db, TimeProvider clock) : ISessionService
{
    public async Task<SessionDto?> StartAsync(Guid quizId, Guid hostUserId, CancellationToken ct)
    {
        var quiz = await db.Quizzes
            .Include(q => q.Questions)
            .FirstOrDefaultAsync(q => q.Id == quizId && q.CreatedByUserId == hostUserId, ct);
        if (quiz is null) return null;

        var session = new QuizSession
        {
            Id               = Guid.NewGuid(),
            QuizId           = quizId,
            HostUserId       = hostUserId,
            StartedAt        = clock.GetUtcNow(),
            Status           = SessionStatus.InProgress,
            PublicShareToken = GenerateShareToken(),
            Answers          = quiz.Questions.Select(q => new Answer
            {
                Id            = Guid.NewGuid(),
                QuestionId    = q.Id,
                AnswerText    = string.Empty,
                IsCorrect     = null,
                PointsAwarded = 0m,
            }).ToList(),
        };

        db.QuizSessions.Add(session);
        await db.SaveChangesAsync(ct);

        return await BuildDtoAsync(session.Id, hostUserId, ct);
    }

    public async Task<SessionDto?> GetByIdAsync(Guid sessionId, Guid hostUserId, CancellationToken ct) =>
        await BuildDtoAsync(sessionId, hostUserId, ct);

    public async Task<SessionResult> RecordAnswerAsync(
        Guid sessionId, Guid questionId, Guid hostUserId,
        RecordAnswerRequest request, CancellationToken ct)
    {
        var session = await LoadOwnedAsync(sessionId, hostUserId, ct);
        if (session is null) return new SessionResult.NotFound();
        if (session.Status != SessionStatus.InProgress)
            return new SessionResult.InvalidState($"Session is {session.Status.Name}; answers can only be recorded while InProgress.");

        var answer = session.Answers.FirstOrDefault(a => a.QuestionId == questionId);
        if (answer is null) return new SessionResult.NotFound();

        answer.AnswerText = request.AnswerText ?? string.Empty;
        await db.SaveChangesAsync(ct);

        var dto = await BuildDtoAsync(sessionId, hostUserId, ct);
        return new SessionResult.Ok(dto!);
    }

    public async Task<SessionResult> RevealAsync(Guid sessionId, Guid hostUserId, CancellationToken ct)
    {
        var session = await LoadOwnedAsync(sessionId, hostUserId, ct);
        if (session is null) return new SessionResult.NotFound();
        if (session.Status != SessionStatus.InProgress)
            return new SessionResult.InvalidState($"Session is already {session.Status.Name}.");

        // Auto-grade MC answers; leave FreeText for the host to mark.
        var byQuestionId = session.Answers.ToDictionary(a => a.QuestionId);
        foreach (var question in session.Quiz!.Questions)
        {
            if (!byQuestionId.TryGetValue(question.Id, out var answer)) continue;
            if (question.Type != QuestionType.MultipleChoice) continue;

            var correct = string.Equals(
                (answer.AnswerText ?? string.Empty).Trim(),
                question.CorrectAnswer.Trim(),
                StringComparison.OrdinalIgnoreCase);
            answer.IsCorrect     = correct;
            answer.PointsAwarded = correct ? 1m : 0m;
        }

        session.Status = SessionStatus.AwaitingReveal;
        await db.SaveChangesAsync(ct);

        var dto = await BuildDtoAsync(sessionId, hostUserId, ct);
        return new SessionResult.Ok(dto!);
    }

    public async Task<SessionResult> GradeAnswerAsync(
        Guid sessionId, Guid questionId, Guid hostUserId,
        GradeAnswerRequest request, CancellationToken ct)
    {
        var session = await LoadOwnedAsync(sessionId, hostUserId, ct);
        if (session is null) return new SessionResult.NotFound();
        if (session.Status != SessionStatus.AwaitingReveal)
            return new SessionResult.InvalidState($"Manual grading requires AwaitingReveal; current status is {session.Status.Name}.");

        var answer = session.Answers.FirstOrDefault(a => a.QuestionId == questionId);
        if (answer is null) return new SessionResult.NotFound();

        var pts = Math.Clamp(request.PointsAwarded, 0m, 1m);
        answer.IsCorrect     = request.IsCorrect;
        answer.PointsAwarded = pts;
        answer.GradingNote   = request.GradingNote;
        await db.SaveChangesAsync(ct);

        var dto = await BuildDtoAsync(sessionId, hostUserId, ct);
        return new SessionResult.Ok(dto!);
    }

    public async Task<SessionResult> CompleteAsync(Guid sessionId, Guid hostUserId, CancellationToken ct)
    {
        var session = await LoadOwnedAsync(sessionId, hostUserId, ct);
        if (session is null) return new SessionResult.NotFound();
        if (session.Status == SessionStatus.Graded)
            return new SessionResult.InvalidState("Session is already complete.");
        if (session.Status != SessionStatus.AwaitingReveal)
            return new SessionResult.InvalidState($"Cannot complete from {session.Status.Name}; reveal first.");
        if (session.Answers.Any(a => a.IsCorrect is null))
            return new SessionResult.InvalidState("Some answers are not yet graded.");

        session.Status      = SessionStatus.Graded;
        session.CompletedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);

        var dto = await BuildDtoAsync(sessionId, hostUserId, ct);
        return new SessionResult.Ok(dto!);
    }

    // ----- helpers -----

    private async Task<QuizSession?> LoadOwnedAsync(Guid sessionId, Guid hostUserId, CancellationToken ct) =>
        await db.QuizSessions
            .Include(s => s.Quiz!).ThenInclude(q => q.Questions)
            .Include(s => s.Answers)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.HostUserId == hostUserId, ct);

    private async Task<SessionDto?> BuildDtoAsync(Guid sessionId, Guid hostUserId, CancellationToken ct)
    {
        var session = await LoadOwnedAsync(sessionId, hostUserId, ct);
        if (session is null) return null;

        var quiz = session.Quiz!;
        var answersByQ = session.Answers.ToDictionary(a => a.QuestionId);
        var hideAnswers = session.Status == SessionStatus.InProgress;

        var questions = quiz.Questions
            .OrderBy(q => q.Order)
            .Select(q =>
            {
                var ans = answersByQ.GetValueOrDefault(q.Id);
                var options = q.OptionsJson is null
                    ? null
                    : JsonSerializer.Deserialize<List<string>>(q.OptionsJson);

                // Shuffle MC options deterministically per (session, question)
                // so AI's positional bias doesn't leak the answer. Same
                // (sessionId, questionId) always produces the same order, so
                // back-navigation and refresh work; different sessions get
                // different orders.
                if (q.Type == QuestionType.MultipleChoice && options is { Count: > 1 })
                {
                    options = ShuffleDeterministic(options, session.Id, q.Id);
                }

                return new SessionQuestionDto(
                    Id:            q.Id,
                    Text:          q.Text,
                    Type:          q.Type.Name,
                    CorrectAnswer: hideAnswers ? null : q.CorrectAnswer,
                    Options:       options,
                    Explanation:   hideAnswers ? null : q.Explanation,
                    Order:         q.Order,
                    Answer: ans is null
                        ? new SessionAnswerDto(string.Empty, null, 0m, null)
                        : new SessionAnswerDto(ans.AnswerText, ans.IsCorrect, ans.PointsAwarded, ans.GradingNote));
            })
            .ToList();

        var score    = session.Answers.Sum(a => a.PointsAwarded);
        var maxScore = quiz.Questions.Count; // 1 point per question

        return new SessionDto(
            Id:               session.Id,
            QuizId:           session.QuizId,
            QuizTitle:        quiz.Title,
            Status:           session.Status.Name,
            StartedAt:        session.StartedAt,
            CompletedAt:      session.CompletedAt,
            PublicShareToken: session.PublicShareToken,
            Questions:        questions,
            Score:            score,
            MaxScore:         maxScore);
    }

    private static string GenerateShareToken()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    /// <summary>
    /// Fisher–Yates shuffle keyed off a deterministic seed derived from
    /// (sessionId, questionId). Avoids HashCode.Combine and Guid.GetHashCode
    /// (both process-randomised) so the shuffle is stable across restarts
    /// and machines for the same key — refresh during play won't reshuffle.
    /// </summary>
    private static List<string> ShuffleDeterministic(List<string> source, Guid sessionId, Guid questionId)
    {
        var seed = DeterministicSeed(sessionId, questionId);
        var rng  = new Random(seed);
        var copy = new List<string>(source);
        for (var i = copy.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (copy[i], copy[j]) = (copy[j], copy[i]);
        }
        return copy;
    }

    private static int DeterministicSeed(Guid a, Guid b)
    {
        Span<byte> ba = stackalloc byte[16];
        Span<byte> bb = stackalloc byte[16];
        a.TryWriteBytes(ba);
        b.TryWriteBytes(bb);

        // XOR the two byte arrays then fold into a 32-bit accumulator.
        // Using 31 as the multiplier (classic string-hash style) — the result
        // doesn't need to be cryptographic, just stable.
        unchecked
        {
            var seed = 0;
            for (var i = 0; i < 16; i++)
            {
                seed = (seed * 31) + (ba[i] ^ bb[i]);
            }
            return seed;
        }
    }
}
