using CitizenTech.Api.Contracts;
using CitizenTech.Api.Errors;
using CitizenTech.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CitizenTech.Api.Controllers;

[ApiController]
[ServiceFilter(typeof(AdminKeyFilter))]
[EnableRateLimiting("admin")]
[ProducesResponseType<ErrorResponse>(400)]
[ProducesResponseType<ErrorResponse>(401)]
[ProducesResponseType<ErrorResponse>(429)]
[ProducesResponseType<ErrorResponse>(503)]
public sealed class AdminController(EvaluationService evaluations, AdminQueryService queries, TimeProvider clock) : ControllerBase
{
    [HttpPost("/api/v1/sessions/{sessionId}/evaluation")]
    [ProducesResponseType<AdminEvaluationResponse>(201)]
    [ProducesResponseType<ErrorResponse>(404)]
    [ProducesResponseType<ErrorResponse>(409)]
    public async Task<IActionResult> Evaluate(string sessionId, EvaluationRequest request, CancellationToken token)
    {
        var result = await evaluations.CreateAsync(AdminInput.SessionId(sessionId), request, token);
        return StatusCode(201, new AdminEvaluationResponse(result.Id, result.SessionId, result.Outcome,
            result.Rating, result.NeededHumanHelp, result.Source, result.CreatedAtUtc));
    }

    [HttpGet("/api/v1/admin/sessions")]
    [ProducesResponseType<SessionListResponse>(200)]
    public async Task<IActionResult> List([FromQuery] string? from, [FromQuery] string? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] bool? hasFailures = null,
        [FromQuery] bool? hasEvaluation = null, [FromQuery] string? outcome = null, CancellationToken token = default)
    {
        AdminInput.Pagination(page, pageSize);
        if (outcome is not null && (!EvaluationOutcomes.IsValid(outcome) || hasEvaluation == false))
            throw new ApiException(400, "INVALID_REQUEST");
        return Ok(await queries.ListAsync(AdminInput.Interval(from, to, clock), page, pageSize, hasFailures, hasEvaluation, outcome, token));
    }

    [HttpGet("/api/v1/admin/sessions/{sessionId}")]
    [ProducesResponseType<SessionDetailResponse>(200)]
    [ProducesResponseType<ErrorResponse>(404)]
    public async Task<IActionResult> Detail(string sessionId, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken token = default)
    {
        AdminInput.Pagination(page, pageSize);
        return Ok(await queries.DetailAsync(AdminInput.SessionId(sessionId), page, pageSize, token));
    }

    [HttpGet("/api/v1/admin/metrics")]
    [ProducesResponseType<MetricsResponse>(200)]
    public async Task<IActionResult> Metrics([FromQuery] string? from, [FromQuery] string? to, CancellationToken token) =>
        Ok(await queries.MetricsAsync(AdminInput.Interval(from, to, clock), token));
}
