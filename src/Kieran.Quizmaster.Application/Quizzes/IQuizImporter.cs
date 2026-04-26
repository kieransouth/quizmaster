using Kieran.Quizmaster.Application.Quizzes.Dtos;

namespace Kieran.Quizmaster.Application.Quizzes;

public interface IQuizImporter
{
    /// <summary>
    /// Streams an import run (free-text source -&gt; structured questions)
    /// using the same event vocabulary as <see cref="IQuizGenerator"/>.
    /// </summary>
    IAsyncEnumerable<GenerationEvent> ImportAsync(
        Guid              userId,
        ImportQuizRequest request,
        CancellationToken cancellationToken);
}
