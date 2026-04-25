namespace Kieran.Quizmaster.Application.Quizzes.Dtos;

public sealed record GenerateQuizRequest(
    string Title,
    IReadOnlyList<TopicRequest> Topics,
    /// <summary>0.0 = all free-text, 1.0 = all multiple choice.</summary>
    double MultipleChoiceFraction,
    bool RunFactCheck,
    string Provider,
    string Model,
    /// <summary>
    /// Optional: provider used for the fact-check pass. Falls back to <see cref="Provider"/>
    /// when null. Picking a different model gives an independent verification rather than
    /// asking the same model to grade its own work.
    /// </summary>
    string? FactCheckProvider = null,
    /// <summary>Optional: model used for the fact-check pass. Falls back to <see cref="Model"/>.</summary>
    string? FactCheckModel = null);
