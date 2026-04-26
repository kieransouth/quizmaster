using System.Security.Claims;
using Kieran.Quizmaster.Api.Sse;
using Kieran.Quizmaster.Application.Quizzes;
using Kieran.Quizmaster.Application.Quizzes.Dtos;
using Kieran.Quizmaster.Domain.Enumerations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kieran.Quizmaster.Api.Controllers;

[ApiController]
[Route("quizzes")]
[Authorize]
public class QuizzesController(
    IQuizGenerator           generator,
    IQuizImporter            importer,
    IQuizJsonImporter        jsonImporter,
    IQuizService             quizService,
    IQuizFactCheckService    factCheckService,
    IQuizQuestionRegenerator regenerator) : ControllerBase
{
    // ----- AI-driven (Phase 5) -----

    /// <summary>
    /// Streams a generation run as Server-Sent Events. Each event is one
    /// line of JSON in a typed envelope (status, question, warning, done,
    /// error).
    /// </summary>
    [HttpPost("generate")]
    public async Task GenerateStream(
        [FromBody] GenerateQuizRequest request,
        CancellationToken              cancellationToken)
    {
        Response.EnableSse();
        if (!TryGetUserId(out var userId))
        {
            await Response.WriteEventAsync(new GenerationEvent.Error("Unauthorized.", false), cancellationToken);
            return;
        }
        await foreach (var evt in generator.GenerateAsync(userId, request, cancellationToken))
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
        if (!TryGetUserId(out var userId))
        {
            await Response.WriteEventAsync(new GenerationEvent.Error("Unauthorized.", false), cancellationToken);
            return;
        }
        await foreach (var evt in importer.ImportAsync(userId, request, cancellationToken))
        {
            await Response.WriteEventAsync(evt, cancellationToken);
        }
    }

    /// <summary>
    /// "Bring your own AI": user pasted JSON they got from an external AI
    /// using the prompt template Quizmaster recommends. We just parse it
    /// and emit the same SSE event vocabulary so the frontend reuses the
    /// review/save flow. No AI calls happen on our side.
    /// </summary>
    [HttpPost("import-json")]
    public async Task ImportJsonStream(
        [FromBody] ImportFromJsonRequest request,
        CancellationToken                cancellationToken)
    {
        Response.EnableSse();
        await foreach (var evt in jsonImporter.ImportFromJsonAsync(request, cancellationToken))
        {
            await Response.WriteEventAsync(evt, cancellationToken);
        }
    }

    // ----- CRUD (Phase 6) -----

    /// <summary>Persist a draft as a new quiz owned by the current user.</summary>
    [HttpPost]
    public async Task<IActionResult> Save([FromBody] DraftQuiz draft, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var id = await quizService.SaveAsync(draft, userId, ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    /// <summary>List the user's quizzes (newest first) for the dashboard.</summary>
    [HttpGet]
    public async Task<IActionResult> ListMine(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var summaries = await quizService.ListMineAsync(userId, ct);
        return Ok(summaries);
    }

    /// <summary>Full quiz detail (questions, topics, etc.) — only if the caller owns it.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var quiz = await quizService.GetByIdAsync(id, userId, ct);
        return quiz is null ? NotFound() : Ok(quiz);
    }

    /// <summary>Replace title + questions. Removes any existing questions not in the request.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateQuizRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var ok = await quizService.UpdateAsync(id, userId, request, ct);
        return ok ? NoContent() : NotFound();
    }

    /// <summary>Delete a quiz (cascade-deletes its topics, questions, and any sessions/answers).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var ok = await quizService.DeleteAsync(id, userId, ct);
        return ok ? NoContent() : NotFound();
    }

    /// <summary>
    /// Generate a single replacement question (NOT persisted — caller saves
    /// via PUT once happy with the result). Other questions in the quiz are
    /// passed to the model to discourage duplicates.
    /// </summary>
    [HttpPost("{id:guid}/regenerate-question/{questionId:guid}")]
    public async Task<IActionResult> RegenerateQuestion(
        Guid id,
        Guid questionId,
        [FromBody] RegenerateQuestionRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            var newQ = await regenerator.RegenerateAsync(id, questionId, userId, request, ct);
            return newQ is null ? NotFound() : Ok(newQ);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ----- Standalone fact-check (decoupled from generation) -----

    /// <summary>
    /// AI fact-check on a pre-save draft. Body carries the draft questions
    /// + chosen provider/model; response is the merged question list with
    /// flags applied. Nothing is persisted.
    /// </summary>
    [HttpPost("fact-check")]
    public async Task<IActionResult> FactCheckDraft(
        [FromBody] FactCheckDraftRequest request,
        CancellationToken                ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!AiProviderKind.TryFromName(request.Provider, out var provider))
            return BadRequest(new { error = $"Unknown provider '{request.Provider}'." });

        try
        {
            var merged = await factCheckService.ApplyAiAsync(
                userId, request.Questions, provider, request.Model, ct);
            return Ok(new FactCheckDraftResponse(merged));
        }
        catch (InvalidOperationException ex)
        {
            // Surfaces from the factory (no-key, disabled provider, model not
            // in allowlist) — those are user-actionable, so 400.
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }

    /// <summary>
    /// BYO fact-check on a pre-save draft. User pasted the model's JSON
    /// response into the request body; we parse and apply it.
    /// </summary>
    [HttpPost("fact-check-json")]
    public IActionResult FactCheckDraftJson(
        [FromBody] FactCheckDraftFromJsonRequest request)
    {
        if (!TryGetUserId(out _)) return Unauthorized();
        try
        {
            var merged = factCheckService.ApplyJson(request.Questions, request.SourceJson);
            return Ok(new FactCheckDraftResponse(merged));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// AI fact-check on a saved quiz. Persists flag changes in place,
    /// returns the refreshed detail DTO.
    /// </summary>
    [HttpPost("{id:guid}/fact-check")]
    public async Task<IActionResult> FactCheckSaved(
        Guid                             id,
        [FromBody] FactCheckSavedRequest request,
        CancellationToken                ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!AiProviderKind.TryFromName(request.Provider, out var provider))
            return BadRequest(new { error = $"Unknown provider '{request.Provider}'." });

        try
        {
            var updated = await factCheckService.ApplyAiToSavedAsync(
                id, userId, provider, request.Model, ct);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }

    /// <summary>BYO fact-check on a saved quiz.</summary>
    [HttpPost("{id:guid}/fact-check-json")]
    public async Task<IActionResult> FactCheckSavedJson(
        Guid                                     id,
        [FromBody] FactCheckSavedFromJsonRequest request,
        CancellationToken                        ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            var updated = await factCheckService.ApplyJsonToSavedAsync(
                id, userId, request.SourceJson, ct);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private bool TryGetUserId(out Guid userId)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out userId);
    }
}
