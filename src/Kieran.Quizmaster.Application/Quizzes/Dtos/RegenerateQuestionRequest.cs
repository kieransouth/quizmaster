namespace Kieran.Quizmaster.Application.Quizzes.Dtos;

/// <summary>
/// Body for POST /quizzes/:id/regenerate-question/:questionId. Provider and
/// model can override what the quiz was originally generated with.
/// </summary>
public sealed record RegenerateQuestionRequest(string Provider, string Model);
