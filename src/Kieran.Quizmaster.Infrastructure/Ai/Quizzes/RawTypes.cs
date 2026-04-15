using System.Text.Json.Serialization;

namespace Kieran.Quizmaster.Infrastructure.Ai.Quizzes;

/// <summary>
/// Mirror of the JSON schema we ask the model for. Internal to the
/// AI pipeline — Application services never see these; they consume
/// validated <see cref="Application.Quizzes.Dtos.DraftQuestion"/> instead.
/// </summary>
internal sealed record RawQuestion(
    [property: JsonPropertyName("topic")]         string? Topic,
    [property: JsonPropertyName("text")]          string  Text,
    [property: JsonPropertyName("type")]          string  Type,
    [property: JsonPropertyName("correctAnswer")] string  CorrectAnswer,
    [property: JsonPropertyName("options")]       IReadOnlyList<string>? Options,
    [property: JsonPropertyName("explanation")]   string? Explanation);

internal sealed record RawQuizResponse(
    [property: JsonPropertyName("questions")] IReadOnlyList<RawQuestion> Questions);

internal sealed record RawFactCheck(
    [property: JsonPropertyName("questionIndex")]    int     QuestionIndex,
    [property: JsonPropertyName("factuallyCorrect")] bool    FactuallyCorrect,
    [property: JsonPropertyName("note")]             string? Note);

internal sealed record RawFactCheckResponse(
    [property: JsonPropertyName("checks")] IReadOnlyList<RawFactCheck> Checks);
