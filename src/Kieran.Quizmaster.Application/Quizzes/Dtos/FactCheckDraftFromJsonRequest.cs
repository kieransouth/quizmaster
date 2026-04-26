namespace Kieran.Quizmaster.Application.Quizzes.Dtos;

/// <summary>
/// Request body for the BYO fact-check on a pre-save draft. The user
/// pasted JSON from an external AI; the server merges its <c>checks</c>
/// into the supplied questions and returns the result. No AI calls.
/// </summary>
public sealed record FactCheckDraftFromJsonRequest(
    IReadOnlyList<DraftQuestion> Questions,
    string                       SourceJson);
