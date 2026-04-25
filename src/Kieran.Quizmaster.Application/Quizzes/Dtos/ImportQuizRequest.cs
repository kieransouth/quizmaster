namespace Kieran.Quizmaster.Application.Quizzes.Dtos;

public sealed record ImportQuizRequest(
    string Title,
    string SourceText,
    bool RunFactCheck,
    string Provider,
    string Model,
    /// <summary>Optional: provider used for the fact-check pass. Falls back to <see cref="Provider"/>.</summary>
    string? FactCheckProvider = null,
    /// <summary>Optional: model used for the fact-check pass. Falls back to <see cref="Model"/>.</summary>
    string? FactCheckModel = null);
