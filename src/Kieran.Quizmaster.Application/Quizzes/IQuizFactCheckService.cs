using Kieran.Quizmaster.Application.Quizzes.Dtos;
using Kieran.Quizmaster.Domain.Enumerations;

namespace Kieran.Quizmaster.Application.Quizzes;

/// <summary>
/// Standalone fact-check entry point — decoupled from the generation
/// pipeline so callers can mix and match generation method (Generate /
/// Import / BYO) with verification method (AI / BYO) and timing
/// (pre-save draft / post-save).
/// </summary>
public interface IQuizFactCheckService
{
    /// <summary>
    /// AI fact-check on a draft — pure transformation, no DB touch.
    /// Returns the merged questions ready for the client to replace its
    /// local draft state with.
    /// </summary>
    Task<IReadOnlyList<DraftQuestion>> ApplyAiAsync(
        Guid                         userId,
        IReadOnlyList<DraftQuestion> questions,
        AiProviderKind               provider,
        string                       model,
        CancellationToken            ct);

    /// <summary>
    /// BYO fact-check on a draft — parses pre-existing JSON the user
    /// pasted from an external AI tool. No AI calls.
    /// Throws <see cref="InvalidOperationException"/> on malformed JSON.
    /// </summary>
    IReadOnlyList<DraftQuestion> ApplyJson(
        IReadOnlyList<DraftQuestion> questions,
        string                       sourceJson);

    /// <summary>
    /// AI fact-check on a saved quiz. Loads the quiz (owner-scoped),
    /// audits, persists the flag changes in place, returns a refreshed
    /// detail DTO. Returns null when the quiz isn't found or isn't
    /// owned by the caller.
    /// </summary>
    Task<QuizDetailDto?> ApplyAiToSavedAsync(
        Guid              quizId,
        Guid              userId,
        AiProviderKind    provider,
        string            model,
        CancellationToken ct);

    /// <summary>
    /// BYO fact-check on a saved quiz. Same load-update-save shape as
    /// <see cref="ApplyAiToSavedAsync"/> but takes user-pasted JSON
    /// instead of calling an AI.
    /// </summary>
    Task<QuizDetailDto?> ApplyJsonToSavedAsync(
        Guid              quizId,
        Guid              userId,
        string            sourceJson,
        CancellationToken ct);
}
