namespace Kieran.Quizmaster.Application.Quizzes.Dtos;

/// <summary>
/// Response from the draft-mode fact-check endpoints. Carries the merged
/// question list with <c>FactCheckFlagged</c> + <c>FactCheckNote</c>
/// applied. The client replaces its local draft questions with this list.
/// </summary>
public sealed record FactCheckDraftResponse(
    IReadOnlyList<DraftQuestion> Questions);
