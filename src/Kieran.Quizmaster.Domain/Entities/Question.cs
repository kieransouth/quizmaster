using Kieran.Quizmaster.Domain.Enumerations;

namespace Kieran.Quizmaster.Domain.Entities;

public class Question
{
    public Guid Id { get; set; }

    public Guid QuizId { get; set; }
    public Quiz? Quiz { get; set; }

    /// <summary>Null for imported questions (no source topic).</summary>
    public Guid? TopicId { get; set; }
    public QuizTopic? Topic { get; set; }

    public string Text { get; set; } = string.Empty;

    public QuestionType Type { get; set; } = QuestionType.FreeText;

    public string CorrectAnswer { get; set; } = string.Empty;

    /// <summary>Serialized options array (JSON) for MultipleChoice questions; null for FreeText.</summary>
    public string? OptionsJson { get; set; }

    public string? Explanation { get; set; }

    public int Order { get; set; }

    /// <summary>Set by the Stage-2 fact-checker when it considers the question dubious.</summary>
    public bool FactCheckFlagged { get; set; }
    public string? FactCheckNote { get; set; }
}
