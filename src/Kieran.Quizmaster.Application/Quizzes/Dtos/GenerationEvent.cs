using System.Text.Json.Serialization;

namespace Kieran.Quizmaster.Application.Quizzes.Dtos;

/// <summary>
/// One event in a generate/import stream. Polymorphic on the "type"
/// discriminator so the frontend can switch on event.type.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Status),   "status")]
[JsonDerivedType(typeof(Question), "question")]
[JsonDerivedType(typeof(Warning),  "warning")]
[JsonDerivedType(typeof(Done),     "done")]
[JsonDerivedType(typeof(Error),    "error")]
public abstract record GenerationEvent
{
    public sealed record Status(string Stage) : GenerationEvent;
    public sealed record Question(DraftQuestion Item) : GenerationEvent;
    public sealed record Warning(string Message) : GenerationEvent;
    public sealed record Done(DraftQuiz Quiz) : GenerationEvent;
    public sealed record Error(string Message, bool Retryable) : GenerationEvent;
}
