using CitizenTech.Api.Contracts;
using CitizenTech.Api.Data;
using CitizenTech.Api.Errors;
using CitizenTech.Api.Models;

namespace CitizenTech.Api.Services;

public sealed class GuidanceService(IGuidanceProvider provider, IGuidanceRecordStore store)
{
    public async Task<GuidanceResponse> GuideAsync(GuidanceRequest request, CancellationToken cancellationToken, TimeSpan elapsed = default)
    {
        if (!request.IsValid()) throw new ApiException(400, "INVALID_REQUEST");
        // Inclui consulta, geração, validação e confirmação. Nunca abre transação durante geração.
        using var total = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var remaining = TimeSpan.FromSeconds(20) - elapsed;
        if (remaining <= TimeSpan.Zero) throw new ApiException(503, "PERSISTENCE_UNAVAILABLE");
        total.CancelAfter(remaining);
        var generationStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            if (await store.ExistsAsync(request.RequestId, total.Token))
                throw new ApiException(409, "REQUEST_ALREADY_RECORDED");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new ApiException(503, "PERSISTENCE_UNAVAILABLE"); }
        catch (PersistenceException) { throw new ApiException(503, "PERSISTENCE_UNAVAILABLE"); }

        GuidanceDecision decision;
        using var generation = CancellationTokenSource.CreateLinkedTokenSource(total.Token);
        // Reserva até 5 s para validar/gravar, mesmo após uma consulta Oracle lenta.
        var availableForGeneration = remaining - System.Diagnostics.Stopwatch.GetElapsedTime(generationStarted) - TimeSpan.FromSeconds(5);
        if (availableForGeneration <= TimeSpan.Zero) throw new ApiException(504, "AI_TIMEOUT");
        generation.CancelAfter(availableForGeneration < TimeSpan.FromSeconds(15) ? availableForGeneration : TimeSpan.FromSeconds(15));
        try
        {
            decision = await provider.GenerateAsync(request, generation.Token);
            generation.Token.ThrowIfCancellationRequested();
            if (decision is null || !decision.IsValidFor(request)) throw new ApiException(502, "AI_UNAVAILABLE");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new ApiException(504, "AI_TIMEOUT"); }
        catch (ApiException) { throw; }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        { throw new ApiException(502, "AI_UNAVAILABLE"); }

        try
        {
            await store.AddAsync(GuidanceRecord.Success(request.RequestId, request.SessionId, decision.Action), total.Token);
            total.Token.ThrowIfCancellationRequested();
        }
        catch (DuplicateRecordException) { throw new ApiException(409, "REQUEST_ALREADY_RECORDED"); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new ApiException(503, "PERSISTENCE_UNAVAILABLE"); }
        catch (PersistenceException) { throw new ApiException(503, "PERSISTENCE_UNAVAILABLE"); }

        return new("1", request.RequestId, request.SessionId, request.Screen!.SnapshotId,
            decision.Action, decision.Instruction, decision.TargetElementId);
    }
}
