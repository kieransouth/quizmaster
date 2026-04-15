using Kieran.Quizmaster.Domain.Enumerations;

namespace Kieran.Quizmaster.Domain.Entities;

public class Quiz
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;

    public QuizSource Source { get; set; } = QuizSource.Generated;

    /// <summary>Original pasted text when <see cref="Source"/> is Imported. Null otherwise.</summary>
    public string? SourceText { get; set; }

    /// <summary>Audit: which provider was used to produce this quiz (e.g. "Ollama").</summary>
    public string ProviderUsed { get; set; } = string.Empty;

    /// <summary>Audit: which model produced this quiz (e.g. "qwen2.5:72b").</summary>
    public string ModelUsed { get; set; } = string.Empty;

    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<QuizTopic> Topics    { get; set; } = new List<QuizTopic>();
    public ICollection<Question>  Questions { get; set; } = new List<Question>();
}
