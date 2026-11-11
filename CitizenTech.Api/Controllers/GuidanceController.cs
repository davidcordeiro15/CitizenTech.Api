using CitizenTech.Api.Contracts;
using CitizenTech.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Diagnostics;

namespace CitizenTech.Api.Controllers;

[ApiController]
[Route("api/v1/guidance")]
public sealed class GuidanceController(GuidanceService service) : ControllerBase
{
    /// <summary>Gera uma orientação pelo provedor configurado e confirma os metadados no Oracle antes do sucesso.</summary>
    [HttpPost]
    [EnableRateLimiting("guidance")]
    [ProducesResponseType<GuidanceResponse>(200)]
    [ProducesResponseType<ErrorResponse>(400)]
    [ProducesResponseType<ErrorResponse>(409)]
    [ProducesResponseType<ErrorResponse>(413)]
    [ProducesResponseType<ErrorResponse>(429)]
    [ProducesResponseType<ErrorResponse>(500)]
    [ProducesResponseType<ErrorResponse>(502)]
    [ProducesResponseType<ErrorResponse>(503)]
    [ProducesResponseType<ErrorResponse>(504)]
    public async Task<ActionResult<GuidanceResponse>> Post(GuidanceRequest request, CancellationToken cancellationToken)
    {
        HttpContext.Items["RequestId"] = request.RequestId;
        var elapsed = HttpContext.Items["StartedAt"] is long started ? Stopwatch.GetElapsedTime(started) : TimeSpan.Zero;
        return Ok(await service.GuideAsync(request, cancellationToken, elapsed));
    }
}

[ApiController]
[Route("api/v1/health")]
public sealed class HealthController : ControllerBase
{
    /// <summary>Verifica somente aplicação e contrato; não acessa Oracle ou IA.</summary>
    [HttpGet]
    [ProducesResponseType<HealthResponse>(200)]
    public ActionResult<HealthResponse> Get() => Ok(new HealthResponse());
}
