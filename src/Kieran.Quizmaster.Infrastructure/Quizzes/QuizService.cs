using System.Text.Json;
using Kieran.Quizmaster.Application.Quizzes;
using Kieran.Quizmaster.Application.Quizzes.Dtos;
using Kieran.Quizmaster.Domain.Entities;
using Kieran.Quizmaster.Domain.Enumerations;
using Kieran.Quizmaster.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kieran.Quizmaster.Infrastructure.Quizzes;

public sealed class QuizService(ApplicationDbContext db, TimeProvider clock) : IQuizService
{
    public async Task<Guid> SaveAsync(DraftQuiz draft, Guid userId, CancellationToken ct)
    {
        var quizId = Guid.NewGuid();

        var topics = draft.Topics
            .Select(t => new QuizTopic
            {
                Id             = Guid.NewGuid(),
                QuizId         = quizId,
                Name           = t.Name,
                RequestedCount = t.Count,
            })
            .ToList();

        var topicIdByName = topics.ToDictionary(
            t => t.Name, t => (Guid?)t.Id, StringComparer.OrdinalIgnoreCase);

        var questions = draft.Questions
            .Select((q, i) => new Question
            {
                Id               = Guid.NewGuid(),
                QuizId           = quizId,
                TopicId          = topicIdByName.GetValueOrDefault(q.Topic ?? string.Empty),
                Text             = q.Text,
                Type             = QuestionType.FromName(q.Type, ignoreCase: true),
                CorrectAnswer    = q.CorrectAnswer,
                OptionsJson      = q.Options is null ? null : JsonSerializer.Serialize(q.Options),
                Explanation      = q.Explanation,
                Order            = i,
                FactCheckFlagged = q.FactCheckFlagged,
                FactCheckNote    = q.FactCheckNote,
            })
            .ToList();

        db.Quizzes.Add(new Quiz
        {
            Id              = quizId,
            Title           = draft.Title,
            Source          = QuizSource.FromName(draft.Source, ignoreCase: true),
            SourceText      = draft.SourceText,
            ProviderUsed    = draft.ProviderUsed,
            ModelUsed       = draft.ModelUsed,
            CreatedByUserId = userId,
            CreatedAt       = clock.GetUtcNow(),
            Topics          = topics,
            Questions       = questions,
        });

        await db.SaveChangesAsync(ct);
        return quizId;
    }

    public async Task<IReadOnlyList<QuizSummaryDto>> ListMineAsync(Guid userId, CancellationToken ct)
    {
        // Fetch then sort in-memory: SQLite (used by tests) can't ORDER BY a
        // DateTimeOffset column; Postgres can but the result list is small
        // enough that the difference doesn't matter.
        var rows = await db.Quizzes
            .AsNoTracking()
            .Where(q => q.CreatedByUserId == userId)
            .Select(q => new QuizSummaryDto(
                q.Id, q.Title, q.Source.Name, q.ProviderUsed, q.ModelUsed,
                q.CreatedAt, q.Questions.Count))
            .ToListAsync(ct);
        return rows.OrderByDescending(r => r.CreatedAt).ToList();
    }

    public async Task<QuizDetailDto?> GetByIdAsync(Guid quizId, Guid userId, CancellationToken ct)
    {
        var quiz = await db.Quizzes
            .AsNoTracking()
            .Include(q => q.Topics)
            .Include(q => q.Questions.OrderBy(qu => qu.Order))
                .ThenInclude(qu => qu.Topic)
            .FirstOrDefaultAsync(q => q.Id == quizId && q.CreatedByUserId == userId, ct);

        if (quiz is null) return null;

        return new QuizDetailDto(
            quiz.Id,
            quiz.Title,
            quiz.Source.Name,
            quiz.ProviderUsed,
            quiz.ModelUsed,
            quiz.SourceText,
            quiz.CreatedAt,
            quiz.Topics
                .Select(t => new TopicRequest(t.Name, t.RequestedCount))
                .ToList(),
            quiz.Questions
                .OrderBy(q => q.Order)
                .Select(ToQuestionDto)
                .ToList());
    }

    public async Task<bool> UpdateAsync(Guid quizId, Guid userId, UpdateQuizRequest request, CancellationToken ct)
    {
        var quiz = await db.Quizzes
            .Include(q => q.Questions)
            .FirstOrDefaultAsync(q => q.Id == quizId && q.CreatedByUserId == userId, ct);
        if (quiz is null) return false;

        quiz.Title = request.Title;

        var keptIds = request.Questions.Select(q => q.Id).ToHashSet();

        // Drop questions removed by the user.
        var toRemove = quiz.Questions.Where(q => !keptIds.Contains(q.Id)).ToList();
        foreach (var q in toRemove) quiz.Questions.Remove(q);

        var byId = quiz.Questions.ToDictionary(q => q.Id);
        foreach (var u in request.Questions)
        {
            if (!byId.TryGetValue(u.Id, out var existing)) continue; // unknown id — ignore
            existing.Text          = u.Text;
            existing.CorrectAnswer = u.CorrectAnswer;
            existing.OptionsJson   = u.Options is null ? null : JsonSerializer.Serialize(u.Options);
            existing.Explanation   = u.Explanation;
            existing.Order         = u.Order;
        }

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid quizId, Guid userId, CancellationToken ct)
    {
        var quiz = await db.Quizzes
            .FirstOrDefaultAsync(q => q.Id == quizId && q.CreatedByUserId == userId, ct);
        if (quiz is null) return false;

        db.Quizzes.Remove(quiz);
        await db.SaveChangesAsync(ct);
        return true;
    }

    internal static QuestionDto ToQuestionDto(Question q) => new(
        Id:               q.Id,
        Topic:            q.Topic?.Name ?? string.Empty,
        Text:             q.Text,
        Type:             q.Type.Name,
        CorrectAnswer:    q.CorrectAnswer,
        Options:          q.OptionsJson is null
                              ? null
                              : JsonSerializer.Deserialize<List<string>>(q.OptionsJson),
        Explanation:      q.Explanation,
        Order:            q.Order,
        FactCheckFlagged: q.FactCheckFlagged,
        FactCheckNote:    q.FactCheckNote);
}
