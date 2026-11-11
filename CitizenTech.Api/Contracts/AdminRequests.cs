using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json.Serialization;
using CitizenTech.Api.Errors;

namespace CitizenTech.Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EvaluationRequest
{
    [JsonRequired, Required] public string? Outcome { get; init; }
    [JsonRequired, Required, Range(1, 5)] public int? Rating { get; init; }
    [JsonRequired, Required] public bool? NeededHumanHelp { get; init; }

    public void Validate()
    {
        if (!EvaluationOutcomes.IsValid(Outcome) || Rating is null or < 1 or > 5 || NeededHumanHelp is null)
            throw new ApiException(400, "INVALID_REQUEST");
    }
}

public static class EvaluationOutcomes
{
    public static bool IsValid(string? value) => value is "completed" or "partially_completed" or "not_completed";
}

public sealed record AdminInterval(DateTime From, DateTime To);

public static class AdminInput
{
    public static Guid SessionId(string value) => Guid.TryParse(value, out var id) && id != Guid.Empty
        ? id : throw new ApiException(400, "INVALID_REQUEST");

    public static void Pagination(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new ApiException(400, "INVALID_REQUEST");
    }

    public static AdminInterval Interval(string? from, string? to, TimeProvider clock)
    {
        if (from is null && to is null)
        {
            var now = clock.GetUtcNow().UtcDateTime;
            return new(now.AddDays(-30), now);
        }
        if (!TryDate(from, out var start) || !TryDate(to, out var end) || start >= end)
            throw new ApiException(400, "INVALID_REQUEST");
        return new(start, end);
    }

    private static bool TryDate(string? value, out DateTime utc)
    {
        utc = default;
        if (value is null || !System.Text.RegularExpressions.Regex.IsMatch(value,
                @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?(Z|[+-]\d{2}:\d{2})$")) return false;
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) return false;
        utc = parsed.UtcDateTime;
        return true;
    }
}
