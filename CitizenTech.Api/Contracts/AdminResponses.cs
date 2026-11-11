namespace CitizenTech.Api.Contracts;

public sealed record AdminEvaluationResponse(Guid Id, Guid SessionId, string Outcome, int Rating,
    bool NeededHumanHelp, string Source, DateTime CreatedAtUtc);

public sealed record AdminSessionResponse(Guid SessionId, DateTime FirstRecordedAtUtc,
    DateTime LastRecordedAtUtc, int TotalGuidanceRecords, int SuccessfulRecords, int FailedRecords,
    bool HasEvaluation, string? Outcome, int? Rating);

public sealed record SessionListResponse(string ContractVersion, DateTime From, DateTime To,
    int Page, int PageSize, int TotalItems, IReadOnlyList<AdminSessionResponse> Items)
{
    public int TotalPages => (int)(((long)TotalItems + PageSize - 1) / PageSize);
}

/// <summary>Only technical metadata is exposed; never page content, prompts, or instructions.</summary>
public sealed record AdminTimelineItem(Guid RequestId, DateTime CreatedAtUtc, string? Action,
    string Result, string? ErrorCode);

public sealed record AdminTimelineResponse(int Page, int PageSize, int TotalItems,
    IReadOnlyList<AdminTimelineItem> Items)
{
    public int TotalPages => (int)(((long)TotalItems + PageSize - 1) / PageSize);
}

public sealed record SessionDetailResponse(string ContractVersion, AdminSessionResponse Session,
    AdminEvaluationResponse? Evaluation, AdminTimelineResponse Timeline);

public sealed record AdminCountResponse(string Value, int Count);

/// <summary>
/// Request statistics use guidance timestamps; evaluation statistics use evaluation timestamps.
/// Percentages are in the range 0–100 and null when their denominator is zero.
/// </summary>
public sealed record MetricsResponse(string ContractVersion, DateTime From, DateTime To,
    int TotalSessions, int TotalGuidanceRecords, int SuccessfulRecords, int FailedRecords,
    decimal? TechnicalSuccessRate, int TotalEvaluations, int CompletedEvaluations,
    int PartiallyCompletedEvaluations, int NotCompletedEvaluations, int NeededHumanHelpEvaluations,
    decimal? CompletionRate, decimal? HumanHelpRate, decimal? AverageRating,
    IReadOnlyList<AdminCountResponse> ErrorCodes);
