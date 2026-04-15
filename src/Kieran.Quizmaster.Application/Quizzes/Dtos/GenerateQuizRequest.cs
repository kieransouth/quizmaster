namespace Kieran.Quizmaster.Application.Quizzes.Dtos;

public sealed record GenerateQuizRequest(
    string Title,
    IReadOnlyList<TopicRequest> Topics,
    /// <summary>0.0 = all free-text, 1.0 = all multiple choice.</summary>
    double MultipleChoiceFraction,
    bool RunFactCheck,
    string Provider,
    string Model);
