using Kieran.Quizmaster.Api.Sse;
using Kieran.Quizmaster.Application.Quizzes;
using Kieran.Quizmaster.Application.Quizzes.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kieran.Quizmaster.Api.Controllers;

[ApiController]
[Route("quizzes")]
[Authorize]
public class QuizzesController(
    IQuizGenerator generator,
    IQuizImporter  importer) : ControllerBase
{
    /// <summary>
    /// Streams a generation run as Server-Sent Events. Each event is one
    /// line of JSON in a typed envelope (status, question, warning, done,
    /// error). Client should consume with EventSource or a fetch-based SSE
    /// reader.
    /// </summary>
    [HttpPost("generate")]
    public async Task GenerateStream(
        [FromBody] GenerateQuizRequest request,
        CancellationToken              cancellationToken)
    {
        Response.EnableSse();
        await foreach (var evt in generator.GenerateAsync(request, cancellationToken))
        {
            await Response.WriteEventAsync(evt, cancellationToken);
        }
    }

    /// <summary>
    /// Same envelope as <see cref="GenerateStream"/>, but the source is a
    /// pasted block of free text instead of topic specs.
    /// </summary>
    [HttpPost("import")]
    public async Task ImportStream(
        [FromBody] ImportQuizRequest request,
        CancellationToken            cancellationToken)
    {
        Response.EnableSse();
        await foreach (var evt in importer.ImportAsync(request, cancellationToken))
        {
            await Response.WriteEventAsync(evt, cancellationToken);
        }
    }
}
