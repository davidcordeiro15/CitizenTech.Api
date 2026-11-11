using System.Data.Common;
using CitizenTech.Api.Models;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;

namespace CitizenTech.Api.Data;

/// <summary>No retries: an interrupted write may already have committed.</summary>
public sealed class OracleGuidanceRecordStore(GuidanceDbContext context) : IGuidanceRecordStore
{
    public async Task<bool> ExistsAsync(Guid requestId, CancellationToken cancellationToken)
    {
        try
        {
            EnsureConnectionConfigured();
            return await context.GuidanceRecords.AsNoTracking()
                .AnyAsync(record => record.RequestId == requestId, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (IsPersistenceFailure(exception))
        {
            throw new PersistenceException(exception);
        }
    }

    public async Task AddAsync(GuidanceRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        try
        {
            EnsureConnectionConfigured();
            context.GuidanceRecords.Add(record);
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (IsUniqueViolation(exception))
        {
            context.Entry(record).State = EntityState.Detached;
            throw new DuplicateRecordException(exception);
        }
        catch (Exception exception) when (IsPersistenceFailure(exception))
        {
            context.Entry(record).State = EntityState.Detached;
            throw new PersistenceException(exception);
        }
    }

    private void EnsureConnectionConfigured()
    {
        if (string.IsNullOrWhiteSpace(context.Database.GetConnectionString()))
            throw new PersistenceException(new InvalidOperationException("Oracle connection is not configured."));
    }

    private static bool IsPersistenceFailure(Exception exception) =>
        exception is DbException or DbUpdateException or TimeoutException or InvalidOperationException;

    // Only Oracle error number 1 (ORA-00001), never message-text matching.
    private static bool IsUniqueViolation(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is OracleException oracle && oracle.Number == 1) return true;
        }
        return false;
    }
}
