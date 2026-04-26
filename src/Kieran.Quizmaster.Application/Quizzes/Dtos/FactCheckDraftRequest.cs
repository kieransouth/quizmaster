namespace Kieran.Quizmaster.Application.Quizzes.Dtos;

/// <summary>
/// Request body for the AI fact-check on a pre-save draft. The server
/// audits the supplied questions against the chosen model and returns the
/// merged list with flags applied. Nothing is persisted.
/// </summary>
public sealed record FactCheckDraftRequest(
    IReadOnlyList<DraftQuestion> Questions,
    string                       Provider,
    string                       Model);
