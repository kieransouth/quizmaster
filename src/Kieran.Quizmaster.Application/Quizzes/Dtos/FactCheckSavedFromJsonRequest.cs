namespace Kieran.Quizmaster.Application.Quizzes.Dtos;

/// <summary>
/// Request body for the BYO fact-check on a saved quiz. Mirrors
/// <see cref="FactCheckDraftFromJsonRequest"/> but the question list is
/// loaded from the database rather than supplied by the client.
/// </summary>
public sealed record FactCheckSavedFromJsonRequest(string SourceJson);
