namespace Kieran.Quizmaster.Application.Quizzes.Dtos;

/// <summary>
/// Request body for the AI fact-check on a saved quiz. Server loads the
/// quiz (owner-scoped), audits its questions, persists the flag changes
/// in place, and returns a refreshed <see cref="QuizDetailDto"/>.
/// </summary>
public sealed record FactCheckSavedRequest(
    string Provider,
    string Model);
