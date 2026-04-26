namespace Kieran.Quizmaster.Application.Quizzes.Dtos;

public sealed record ImportQuizRequest(
    string Title,
    string SourceText,
    string Provider,
    string Model);
