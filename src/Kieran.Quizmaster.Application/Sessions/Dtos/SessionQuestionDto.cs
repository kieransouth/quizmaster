namespace Kieran.Quizmaster.Application.Sessions.Dtos;

/// <summary>
/// Question shape served inside a session. <see cref="CorrectAnswer"/>
/// and <see cref="Explanation"/> are deliberately null while the session
/// is still <c>InProgress</c> — the host is playing along and shouldn't
/// see the answer until reveal.
/// </summary>
public sealed record SessionQuestionDto(
    Guid Id,
    string Text,
    string Type,
    string? CorrectAnswer,
    IReadOnlyList<string>? Options,
    string? Explanation,
    int Order,
    SessionAnswerDto Answer);
