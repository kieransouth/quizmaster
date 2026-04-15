using Ardalis.SmartEnum;

namespace Kieran.Quizmaster.Domain.Enumerations;

public sealed class SessionStatus : SmartEnum<SessionStatus>
{
    public static readonly SessionStatus InProgress     = new(nameof(InProgress), 1);
    public static readonly SessionStatus AwaitingReveal = new(nameof(AwaitingReveal), 2);
    public static readonly SessionStatus Graded         = new(nameof(Graded), 3);

    private SessionStatus(string name, int value) : base(name, value) { }
}
