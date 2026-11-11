using CitizenTech.Api.Models;

namespace CitizenTech.Api.Data;

public interface IGuidanceRecordStore
{
    Task<bool> ExistsAsync(Guid requestId, CancellationToken cancellationToken);
    Task AddAsync(GuidanceRecord record, CancellationToken cancellationToken);
}
