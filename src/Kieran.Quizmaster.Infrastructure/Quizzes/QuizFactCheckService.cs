using System.Text.Json;
using Kieran.Quizmaster.Application.Quizzes;
using Kieran.Quizmaster.Application.Quizzes.Dtos;
using Kieran.Quizmaster.Domain.Enumerations;
using Kieran.Quizmaster.Infrastructure.Ai.Quizzes;
using Kieran.Quizmaster.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kieran.Quizmaster.Infrastructure.Quizzes;

/// <summary>
/// Standalone fact-check service. Replaces the in-stream fact-check that
/// used to live inside <see cref="QuizGenerator"/>; the AI path delegates
/// to <see cref="IFactChecker"/>, the BYO path delegates to
/// <see cref="FactCheckJsonMerger"/>. Saved-quiz variants run the same
/// transformation against the persisted question rows and write flag
/// updates back in place.
/// </summary>
public sealed class QuizFactCheckService(
    IFactChecker         factChecker,
    IQuizService         quizService,
    ApplicationDbContext db) : IQuizFactCheckService
{
    public async Task<IReadOnlyList<DraftQuestion>> ApplyAiAsync(
        Guid                         userId,
        IReadOnlyList<DraftQuestion> questions,
        AiProviderKind               provider,
        string                       model,
        CancellationToken            ct)
    {
        if (questions.Count == 0) return questions;

        // FactChecker takes a DraftQuiz envelope but only inspects Questions.
        var envelope = new DraftQuiz(
            Title:        string.Empty,
            Source:       "Generated",
            ProviderUsed: provider.Name,
            ModelUsed:    model,
            Topics:       [],
            SourceText:   null,
            Questions:    questions);

        var checkedDraft = await factChecker.CheckAsync(userId, envelope, provider, model, ct);
        return checkedDraft.Questions;
    }

    public IReadOnlyList<DraftQuestion> ApplyJson(
        IReadOnlyList<DraftQuestion> questions,
        string                       sourceJson)
        => FactCheckJsonMerger.Apply(questions, sourceJson);

    public async Task<QuizDetailDto?> ApplyAiToSavedAsync(
        Guid              quizId,
        Guid              userId,
        AiProviderKind    provider,
        string            model,
        CancellationToken ct)
    {
        var quiz = await LoadOwnedAsync(quizId, userId, ct);
        if (quiz is null) return null;

        var drafts = quiz.Questions
            .OrderBy(q => q.Order)
            .Select(QuestionToDraft)
            .ToList();

        var merged = await ApplyAiAsync(userId, drafts, provider, model, ct);
        await PersistFlagsAsync(quiz.Id, merged, ct);

        return await ReloadDetailAsync(quizId, userId, ct);
    }

    public async Task<QuizDetailDto?> ApplyJsonToSavedAsync(
        Guid              quizId,
        Guid              userId,
        string            sourceJson,
        CancellationToken ct)
    {
        var quiz = await LoadOwnedAsync(quizId, userId, ct);
        if (quiz is null) return null;

        var drafts = quiz.Questions
            .OrderBy(q => q.Order)
            .Select(QuestionToDraft)
            .ToList();

        var merged = ApplyJson(drafts, sourceJson);
        await PersistFlagsAsync(quiz.Id, merged, ct);

        return await ReloadDetailAsync(quizId, userId, ct);
    }

    private Task<Domain.Entities.Quiz?> LoadOwnedAsync(Guid quizId, Guid userId, CancellationToken ct) =>
        db.Quizzes
          .Include(q => q.Questions)
          .FirstOrDefaultAsync(q => q.Id == quizId && q.CreatedByUserId == userId, ct);

    private async Task PersistFlagsAsync(
        Guid                         quizId,
        IReadOnlyList<DraftQuestion> merged,
        CancellationToken            ct)
    {
        // Re-fetch tracked entities (we'll mutate them). Order by Order so the
        // index alignment with `merged` is correct.
        var tracked = await db.Questions
            .Where(q => q.QuizId == quizId)
            .OrderBy(q => q.Order)
            .ToListAsync(ct);

        for (var i = 0; i < tracked.Count && i < merged.Count; i++)
        {
            tracked[i].FactCheckFlagged = merged[i].FactCheckFlagged;
            tracked[i].FactCheckNote    = merged[i].FactCheckNote;
        }

        await db.SaveChangesAsync(ct);
    }

    private Task<QuizDetailDto?> ReloadDetailAsync(Guid quizId, Guid userId, CancellationToken ct) =>
        quizService.GetByIdAsync(quizId, userId, ct);

    private static DraftQuestion QuestionToDraft(Domain.Entities.Question q) => new(
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
