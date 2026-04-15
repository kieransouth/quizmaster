using Ardalis.SmartEnum;

namespace Kieran.Quizmaster.Domain.Enumerations;

public sealed class QuestionType : SmartEnum<QuestionType>
{
    public static readonly QuestionType MultipleChoice = new(nameof(MultipleChoice), 1);
    public static readonly QuestionType FreeText       = new(nameof(FreeText), 2);

    private QuestionType(string name, int value) : base(name, value) { }
}
