using CitizenTech.Api.Models;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;

namespace CitizenTech.Api.Data;

public interface ISessionEvaluationStore
{
    Task<bool> SessionExistsAsync(Guid sessionId, CancellationToken token);
    Task<bool> EvaluationExistsAsync(Guid sessionId, CancellationToken token);
    Task AddAsync(SessionEvaluation evaluation, CancellationToken token);
}

public sealed class SessionEvaluationStore(GuidanceDbContext context) : ISessionEvaluationStore
{
    public Task<bool> SessionExistsAsync(Guid sessionId, CancellationToken token) =>
        context.GuidanceRecords.AsNoTracking().AnyAsync(x => x.SessionId == sessionId, token);
    public Task<bool> EvaluationExistsAsync(Guid sessionId, CancellationToken token) =>
        context.SessionEvaluations.AsNoTracking().AnyAsync(x => x.SessionId == sessionId, token);

    public async Task AddAsync(SessionEvaluation evaluation, CancellationToken token)
    {
        context.SessionEvaluations.Add(evaluation);
        try { await context.SaveChangesAsync(token); }
        catch (Exception exception) when (IsUniqueViolation(exception))
        {
            context.Entry(evaluation).State = EntityState.Detached;
            throw new DuplicateRecordException(exception);
        }
        catch
        {
            context.Entry(evaluation).State = EntityState.Detached;
            throw;
        }
    }

    private static bool IsUniqueViolation(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is OracleException { Number: 1 }) return true;
        return false;
    }
}
