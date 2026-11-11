using CitizenTech.Api.Contracts;
using CitizenTech.Api.Data;
using CitizenTech.Api.Errors;
using CitizenTech.Api.Models;

namespace CitizenTech.Api.Services;

public sealed class EvaluationService(ISessionEvaluationStore store, TimeProvider clock)
{
    public async Task<SessionEvaluation> CreateAsync(Guid sessionId, EvaluationRequest request, CancellationToken token)
    {
        request.Validate();
        if (sessionId == Guid.Empty) throw new ApiException(400, "INVALID_REQUEST");
        try
        {
            if (!await store.SessionExistsAsync(sessionId, token)) throw new ApiException(404, "SESSION_NOT_FOUND");
            if (await store.EvaluationExistsAsync(sessionId, token)) throw new ApiException(409, "SESSION_ALREADY_EVALUATED");
            var evaluation = new SessionEvaluation(sessionId, request.Outcome!, request.Rating!.Value,
                request.NeededHumanHelp!.Value, clock.GetUtcNow().UtcDateTime);
            await store.AddAsync(evaluation, token);
            return evaluation;
        }
        catch (ApiException) { throw; }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (DuplicateRecordException) { throw new ApiException(409, "SESSION_ALREADY_EVALUATED"); }
        catch (Exception) { throw new ApiException(503, "PERSISTENCE_UNAVAILABLE"); }
    }
}
