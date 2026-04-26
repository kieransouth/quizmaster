using Kieran.Quizmaster.Application.Quizzes.Dtos;

namespace Kieran.Quizmaster.Application.Quizzes;

public interface IQuizGenerator
{
    /// <summary>
    /// Streams a generation run as a series of events (status, question,
    /// warning, done, error). Caller is responsible for the SSE wire format.
    /// </summary>
    IAsyncEnumerable<GenerationEvent> GenerateAsync(
        Guid                 userId,
        GenerateQuizRequest  request,
        CancellationToken    cancellationToken);
}
