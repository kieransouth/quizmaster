namespace Kieran.Quizmaster.Domain.Entities;

public class QuizTopic
{
    public Guid Id { get; set; }

    public Guid QuizId { get; set; }
    public Quiz? Quiz { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Number of questions the user asked the AI to generate for this topic.</summary>
    public int RequestedCount { get; set; }
}
