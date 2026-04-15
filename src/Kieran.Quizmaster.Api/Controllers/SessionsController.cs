using System.Security.Claims;
using Kieran.Quizmaster.Application.Sessions;
using Kieran.Quizmaster.Application.Sessions.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kieran.Quizmaster.Api.Controllers;

[ApiController]
[Route("sessions")]
[Authorize]
public class SessionsController(ISessionService sessions) : ControllerBase
{
    /// <summary>Start a new play session for a quiz the caller owns.</summary>
    [HttpPost]
    public async Task<IActionResult> Start([FromBody] StartSessionRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var session = await sessions.StartAsync(request.QuizId, userId, ct);
        return session is null ? NotFound() : CreatedAtAction(nameof(GetById), new { id = session.Id }, session);
    }

    /// <summary>Full session detail (questions + answers + score).</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var session = await sessions.GetByIdAsync(id, userId, ct);
        return session is null ? NotFound() : Ok(session);
    }

    /// <summary>Idempotent: upsert the team's answer to one question. Only valid while InProgress.</summary>
    [HttpPut("{id:guid}/answers/{questionId:guid}")]
    public async Task<IActionResult> RecordAnswer(
        Guid id, Guid questionId,
        [FromBody] RecordAnswerRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return Map(await sessions.RecordAnswerAsync(id, questionId, userId, request, ct));
    }

    /// <summary>Flip to AwaitingReveal and auto-grade MultipleChoice answers.</summary>
    [HttpPost("{id:guid}/reveal")]
    public async Task<IActionResult> Reveal(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return Map(await sessions.RevealAsync(id, userId, ct));
    }

    /// <summary>Manual grade for one (typically free-text) answer.</summary>
    [HttpPut("{id:guid}/answers/{questionId:guid}/grade")]
    public async Task<IActionResult> GradeAnswer(
        Guid id, Guid questionId,
        [FromBody] GradeAnswerRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return Map(await sessions.GradeAnswerAsync(id, questionId, userId, request, ct));
    }

    /// <summary>Mark the session Graded. Rejects if any answer is still ungraded.</summary>
    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return Map(await sessions.CompleteAsync(id, userId, ct));
    }

    private IActionResult Map(SessionResult result) => result switch
    {
        SessionResult.Ok ok                  => Ok(ok.Session),
        SessionResult.NotFound               => NotFound(),
        SessionResult.InvalidState invalid   => BadRequest(new { error = invalid.Reason }),
        _                                    => StatusCode(500),
    };

    private bool TryGetUserId(out Guid userId)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out userId);
    }
}
