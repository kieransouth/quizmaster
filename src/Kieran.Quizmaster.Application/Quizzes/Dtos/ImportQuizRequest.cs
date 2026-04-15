namespace Kieran.Quizmaster.Application.Quizzes.Dtos;

public sealed record ImportQuizRequest(
    string Title,
    string SourceText,
    bool RunFactCheck,
    string Provider,
    string Model);
