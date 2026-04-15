using Ardalis.SmartEnum;

namespace Kieran.Quizmaster.Domain.Enumerations;

public sealed class QuizSource : SmartEnum<QuizSource>
{
    public static readonly QuizSource Generated = new(nameof(Generated), 1);
    public static readonly QuizSource Imported  = new(nameof(Imported), 2);

    private QuizSource(string name, int value) : base(name, value) { }
}
