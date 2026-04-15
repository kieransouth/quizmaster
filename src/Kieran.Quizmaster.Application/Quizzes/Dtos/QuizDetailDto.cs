namespace Kieran.Quizmaster.Application.Quizzes.Dtos;

public sealed record QuizDetailDto(
    Guid Id,
    string Title,
    string Source,
    string ProviderUsed,
    string ModelUsed,
    string? SourceText,
    DateTimeOffset CreatedAt,
    IReadOnlyList<TopicRequest> Topics,
    IReadOnlyList<QuestionDto> Questions);
