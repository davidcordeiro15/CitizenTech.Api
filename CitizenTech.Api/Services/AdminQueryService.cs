using CitizenTech.Api.Contracts;
using CitizenTech.Api.Data;
using CitizenTech.Api.Errors;
using Microsoft.EntityFrameworkCore;

namespace CitizenTech.Api.Services;

public sealed class AdminQueryService(GuidanceDbContext db)
{
    public Task<SessionListResponse> ListAsync(AdminInterval interval, int page, int pageSize,
        bool? hasFailures, bool? hasEvaluation, string? outcome, CancellationToken cancellationToken) =>
        ReadAsync(async () =>
        {
            AdminInput.Pagination(page, pageSize);
            ValidateInterval(interval);
            if (outcome is not null && (!EvaluationOutcomes.IsValid(outcome) || hasEvaluation == false))
                throw new ApiException(400, "INVALID_REQUEST");

            // Select by activity in the window, but aggregate the entire session history.
            var selected = db.GuidanceRecords.AsNoTracking()
                .Where(x => x.CreatedAtUtc >= interval.From && x.CreatedAtUtc < interval.To)
                .Select(x => x.SessionId).Distinct();
            var query = Summaries().Where(x => selected.Contains(x.SessionId));
            if (hasFailures.HasValue)
                query = query.Where(x => (x.FailedRequests > 0) == hasFailures.Value);

            var joined = from session in query
                         join evaluation in db.SessionEvaluations.AsNoTracking()
                             on session.SessionId equals evaluation.SessionId into evaluations
                         from evaluation in evaluations.DefaultIfEmpty()
                         select new { Session = session, Evaluation = evaluation };
            if (hasEvaluation.HasValue)
                joined = joined.Where(x => (x.Evaluation != null) == hasEvaluation.Value);
            if (outcome is not null)
                joined = joined.Where(x => x.Evaluation != null && x.Evaluation.Outcome == outcome);

            var total = await joined.CountAsync(cancellationToken);
            var rows = await joined.OrderByDescending(x => x.Session.LastActivityAtUtc)
                .ThenBy(x => x.Session.SessionId).Skip((page - 1) * pageSize).Take(pageSize)
                .Select(x => new AdminSessionResponse(x.Session.SessionId,
                    x.Session.FirstActivityAtUtc, x.Session.LastActivityAtUtc, x.Session.TotalRequests,
                    x.Session.SuccessfulRequests, x.Session.FailedRequests, x.Evaluation != null,
                    x.Evaluation == null ? null : x.Evaluation.Outcome,
                    x.Evaluation == null ? null : (int?)x.Evaluation.Rating))
                .ToListAsync(cancellationToken);
            return new SessionListResponse("1", interval.From, interval.To, page, pageSize, total, rows);
        }, cancellationToken);

    public Task<SessionDetailResponse> DetailAsync(Guid sessionId, int page, int pageSize,
        CancellationToken cancellationToken) => ReadAsync(async () =>
        {
            AdminInput.Pagination(page, pageSize);
            var summary = await Summaries().SingleOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);
            if (summary is null) throw new ApiException(404, "SESSION_NOT_FOUND");
            var evaluation = await db.SessionEvaluations.AsNoTracking()
                .Where(x => x.SessionId == sessionId)
                .Select(x => new AdminEvaluationResponse(x.Id, x.SessionId, x.Outcome, x.Rating,
                    x.NeededHumanHelp, x.Source, x.CreatedAtUtc)).SingleOrDefaultAsync(cancellationToken);
            var timeline = await db.GuidanceRecords.AsNoTracking().Where(x => x.SessionId == sessionId)
                .OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.RequestId)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(x => new AdminTimelineItem(x.RequestId, x.CreatedAtUtc, x.Action, x.Result, x.ErrorCode))
                .ToListAsync(cancellationToken);
            return new SessionDetailResponse("1", new AdminSessionResponse(summary.SessionId,
                summary.FirstActivityAtUtc, summary.LastActivityAtUtc, summary.TotalRequests,
                summary.SuccessfulRequests, summary.FailedRequests, evaluation is not null,
                evaluation?.Outcome, evaluation?.Rating), evaluation,
                new AdminTimelineResponse(page, pageSize, summary.TotalRequests, timeline));
        }, cancellationToken);

    public Task<MetricsResponse> MetricsAsync(AdminInterval interval, CancellationToken cancellationToken) =>
        ReadAsync(async () =>
        {
            ValidateInterval(interval);
            var records = db.GuidanceRecords.AsNoTracking()
                .Where(x => x.CreatedAtUtc >= interval.From && x.CreatedAtUtc < interval.To);
            var evaluations = db.SessionEvaluations.AsNoTracking()
                .Where(x => x.CreatedAtUtc >= interval.From && x.CreatedAtUtc < interval.To);

            var requests = await records.GroupBy(x => 1).Select(g => new
            {
                Total = g.Count(),
                Success = g.Count(x => x.Result == "success"),
                Failure = g.Count(x => x.Result == "failure")
            }).SingleOrDefaultAsync(cancellationToken);
            var sessions = await records.Select(x => x.SessionId).Distinct().CountAsync(cancellationToken);
            var ratings = await evaluations.GroupBy(x => 1).Select(g => new
            {
                Total = g.Count(),
                Completed = g.Count(x => x.Outcome == "completed"),
                Partial = g.Count(x => x.Outcome == "partially_completed"),
                NotCompleted = g.Count(x => x.Outcome == "not_completed"),
                HumanHelp = g.Count(x => x.NeededHumanHelp),
                RatingSum = g.Sum(x => (long)x.Rating)
            }).SingleOrDefaultAsync(cancellationToken);
            var errors = await records.Where(x => x.Result == "failure" && x.ErrorCode != null)
                .GroupBy(x => x.ErrorCode).OrderBy(g => g.Key)
                .Select(g => new AdminCountResponse(g.Key!, g.Count())).ToListAsync(cancellationToken);

            var totalEvaluations = ratings?.Total ?? 0;
            return new MetricsResponse("1", interval.From, interval.To, sessions,
                requests?.Total ?? 0, requests?.Success ?? 0, requests?.Failure ?? 0,
                Percent(requests?.Success ?? 0, requests?.Total ?? 0), totalEvaluations,
                ratings?.Completed ?? 0, ratings?.Partial ?? 0, ratings?.NotCompleted ?? 0,
                ratings?.HumanHelp ?? 0,
                Percent(ratings?.Completed ?? 0, totalEvaluations),
                Percent(ratings?.HumanHelp ?? 0, totalEvaluations),
                totalEvaluations == 0 ? null : Math.Round((decimal)ratings!.RatingSum / totalEvaluations, 2,
                    MidpointRounding.AwayFromZero), errors);
        }, cancellationToken);

    private IQueryable<SessionSummary> Summaries() => db.GuidanceRecords.AsNoTracking()
        .GroupBy(x => x.SessionId).Select(g => new SessionSummary
        {
            SessionId = g.Key,
            FirstActivityAtUtc = g.Min(x => x.CreatedAtUtc),
            LastActivityAtUtc = g.Max(x => x.CreatedAtUtc),
            TotalRequests = g.Count(),
            SuccessfulRequests = g.Count(x => x.Result == "success"),
            FailedRequests = g.Count(x => x.Result == "failure")
        });

    public static decimal? Percent(int numerator, int denominator) => denominator == 0
        ? null : Math.Round(100m * numerator / denominator, 2, MidpointRounding.AwayFromZero);

    private static void ValidateInterval(AdminInterval interval)
    {
        if (interval.From >= interval.To) throw new ApiException(400, "INVALID_REQUEST");
    }

    private static async Task<T> ReadAsync<T>(Func<Task<T>> query, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await query();
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (ApiException) { throw; }
        catch (Exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new ApiException(503, "PERSISTENCE_UNAVAILABLE");
        }
    }

    private sealed class SessionSummary
    {
        public Guid SessionId { get; init; }
        public DateTime FirstActivityAtUtc { get; init; }
        public DateTime LastActivityAtUtc { get; init; }
        public int TotalRequests { get; init; }
        public int SuccessfulRequests { get; init; }
        public int FailedRequests { get; init; }
    }
}
