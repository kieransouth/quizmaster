namespace Kieran.Quizmaster.Application.Quizzes.Dtos;

/// <summary>
/// Replaces a quiz's title and questions in one shot. Question identity is
/// preserved by <see cref="UpdateQuestionRequest.Id"/> so we update in
/// place where possible (so QuizSession answers keep working). Any
/// existing question whose Id is missing from the request gets deleted.
/// </summary>
public sealed record UpdateQuizRequest(
    string Title,
    IReadOnlyList<UpdateQuestionRequest> Questions);

public sealed record UpdateQuestionRequest(
    Guid Id,
    string Text,
    string CorrectAnswer,
    IReadOnlyList<string>? Options,
    string? Explanation,
    int Order);
