using Kieran.Quizmaster.Domain.Enumerations;

namespace Kieran.Quizmaster.Domain.Entities;

public class QuizSession
{
    public Guid Id { get; set; }

    public Guid QuizId { get; set; }
    public Quiz? Quiz { get; set; }

    public Guid HostUserId { get; set; }
    public User? HostUser { get; set; }

    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public SessionStatus Status { get; set; } = SessionStatus.InProgress;

    /// <summary>Per-session share token for the public summary at /share/:token.</summary>
    public string PublicShareToken { get; set; } = string.Empty;

    public ICollection<Answer> Answers { get; set; } = new List<Answer>();
}
