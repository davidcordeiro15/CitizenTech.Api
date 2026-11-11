using CitizenTech.Api.Contracts;
using CitizenTech.Api.Data;
using CitizenTech.Api.Errors;
using CitizenTech.Api.Models;
using CitizenTech.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CitizenTech.Api.Tests;

public sealed class AdminQueryTests
{
    private static readonly DateTime From = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly AdminInterval Interval = new(From, From.AddDays(1));
    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");

    [Fact]
    public async Task Metrics_EmptyDatabase_ReturnsZeroCountsAndUndefinedRates()
    {
        await using var fixture = await Database.CreateAsync();
        var result = await fixture.Service.MetricsAsync(Interval, default);

        Assert.Equal("1", result.ContractVersion);
        Assert.Equal(Interval.From, result.From);
        Assert.Equal(Interval.To, result.To);
        Assert.Equal(0, result.TotalSessions);
        Assert.Equal(0, result.TotalGuidanceRecords);
        Assert.Equal(0, result.SuccessfulRecords);
        Assert.Equal(0, result.FailedRecords);
        Assert.Equal(0, result.TotalEvaluations);
        Assert.Equal(0, result.CompletedEvaluations);
        Assert.Equal(0, result.PartiallyCompletedEvaluations);
        Assert.Equal(0, result.NotCompletedEvaluations);
        Assert.Equal(0, result.NeededHumanHelpEvaluations);
        Assert.Null(result.TechnicalSuccessRate);
        Assert.Null(result.CompletionRate);
        Assert.Null(result.HumanHelpRate);
        Assert.Null(result.AverageRating);
        Assert.Empty(result.ErrorCodes);
    }

    [Fact]
    public async Task Metrics_UsesIndependentTimestampPopulationsAndHalfOpenInterval()
    {
        await using var fixture = await Database.CreateAsync();
        await fixture.SeedAsync(
            Record(1, 1, From), Record(2, 1, From.AddHours(1), "B_ERROR"),
            Record(3, 2, From.AddHours(2), "A_ERROR"), Record(4, 2, From.AddHours(3), "B_ERROR"),
            Record(5, 1, From.AddTicks(-1)), Record(6, 3, Interval.To),
            // Evaluation-only sessions contribute to evaluation statistics, not guidance sessions.
            Evaluation(4, From, "completed", 5, true),
            Evaluation(5, From.AddHours(2), "partially_completed", 3, false),
            Evaluation(6, From.AddHours(3), "not_completed", 2, true),
            Evaluation(1, From.AddTicks(-1), "completed", 1, false),
            Evaluation(2, Interval.To, "completed", 1, false));

        var result = await fixture.Service.MetricsAsync(Interval, default);

        Assert.Equal(2, result.TotalSessions);
        Assert.Equal(4, result.TotalGuidanceRecords);
        Assert.Equal(1, result.SuccessfulRecords);
        Assert.Equal(3, result.FailedRecords);
        Assert.Equal(25m, result.TechnicalSuccessRate);
        Assert.Equal(3, result.TotalEvaluations);
        Assert.Equal(1, result.CompletedEvaluations);
        Assert.Equal(1, result.PartiallyCompletedEvaluations);
        Assert.Equal(1, result.NotCompletedEvaluations);
        Assert.Equal(2, result.NeededHumanHelpEvaluations);
        Assert.Equal(33.33m, result.CompletionRate);
        Assert.Equal(66.67m, result.HumanHelpRate);
        Assert.Equal(3.33m, result.AverageRating);
        Assert.Equal(new[] { new AdminCountResponse("A_ERROR", 1), new AdminCountResponse("B_ERROR", 2) }, result.ErrorCodes);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Metrics_OnlyOnePopulation_DoesNotInventOtherPopulation(bool guidanceOnly)
    {
        await using var fixture = await Database.CreateAsync();
        await fixture.SeedAsync(guidanceOnly ? Record(1, 1, From) : Evaluation(1, From, "not_completed", 1, false));
        var result = await fixture.Service.MetricsAsync(Interval, default);

        Assert.Equal(guidanceOnly ? 1 : 0, result.TotalGuidanceRecords);
        Assert.Equal(guidanceOnly ? 0 : 1, result.TotalEvaluations);
        Assert.Equal(guidanceOnly ? 100m : (decimal?)null, result.TechnicalSuccessRate);
        Assert.Equal(guidanceOnly ? null : (decimal?)0m, result.CompletionRate);
        Assert.Equal(guidanceOnly ? null : (decimal?)0m, result.HumanHelpRate);
        Assert.Equal(guidanceOnly ? null : (decimal?)1m, result.AverageRating);
    }

    [Fact]
    public async Task List_SelectsByActivityButSummarizesEntireHistoryAndEvaluation()
    {
        await using var fixture = await Database.CreateAsync();
        await fixture.SeedAsync(
            Record(1, 1, From.AddDays(-2), "OLD_FAILURE"), Record(2, 1, From),
            Record(3, 1, From.AddDays(2)), Record(4, 2, Interval.To),
            Record(5, 3, From.AddTicks(-1)),
            Evaluation(1, From.AddDays(3), "completed", 5, true));

        var result = await fixture.Service.ListAsync(Interval, 1, 10, null, null, null, default);
        var session = Assert.Single(result.Items);
        Assert.Equal(1, result.TotalItems);
        Assert.Equal(1, result.TotalPages);
        Assert.Equal(Id(1), session.SessionId);
        Assert.Equal(From.AddDays(-2), session.FirstRecordedAtUtc);
        Assert.Equal(From.AddDays(2), session.LastRecordedAtUtc);
        Assert.Equal(DateTimeKind.Utc, session.FirstRecordedAtUtc.Kind);
        Assert.Equal(3, session.TotalGuidanceRecords);
        Assert.Equal(2, session.SuccessfulRecords);
        Assert.Equal(1, session.FailedRecords);
        Assert.True(session.HasEvaluation);
        Assert.Equal("completed", session.Outcome);
        Assert.Equal(5, session.Rating);
    }

    [Theory]
    [InlineData(true, null, null, new[] { 1, 3 })]
    [InlineData(false, null, null, new[] { 2, 4 })]
    [InlineData(null, true, null, new[] { 1, 2 })]
    [InlineData(null, false, null, new[] { 3, 4 })]
    [InlineData(null, null, "completed", new[] { 1 })]
    [InlineData(false, true, "partially_completed", new[] { 2 })]
    [InlineData(true, false, null, new[] { 3 })]
    public async Task List_FiltersFailuresEvaluationAndOutcome(bool? failures, bool? evaluated, string? outcome, int[] expected)
    {
        await using var fixture = await Database.CreateAsync();
        await fixture.SeedAsync(
            Record(1, 1, From), Record(2, 2, From), Record(3, 3, From, "ERROR"), Record(4, 4, From),
            Record(5, 1, From.AddDays(-1), "OLD_ERROR"),
            Evaluation(1, From.AddDays(2), "completed", 5, false),
            Evaluation(2, From.AddDays(-2), "partially_completed", 3, true));

        var result = await fixture.Service.ListAsync(Interval, 1, 10, failures, evaluated, outcome, default);
        Assert.Equal(expected.Select(Id), result.Items.Select(x => x.SessionId));
        Assert.Equal(expected.Length, result.TotalItems);
        foreach (var item in result.Items.Where(x => !x.HasEvaluation))
        {
            Assert.Null(item.Outcome);
            Assert.Null(item.Rating);
        }
    }

    [Fact]
    public async Task List_PaginationOrdersByLastActivityThenSessionId()
    {
        await using var fixture = await Database.CreateAsync();
        await fixture.SeedAsync(Record(1, 3, From), Record(2, 2, From), Record(3, 1, From), Record(4, 4, From.AddHours(1)));
        var first = await fixture.Service.ListAsync(Interval, 1, 2, null, null, null, default);
        var second = await fixture.Service.ListAsync(Interval, 2, 2, null, null, null, default);
        var beyond = await fixture.Service.ListAsync(Interval, 3, 2, null, null, null, default);

        Assert.Equal(new[] { Id(4), Id(1) }, first.Items.Select(x => x.SessionId));
        Assert.Equal(new[] { Id(2), Id(3) }, second.Items.Select(x => x.SessionId));
        Assert.Equal(4, second.TotalItems);
        Assert.Equal(2, second.TotalPages);
        Assert.Equal(2, second.Page);
        Assert.Equal(2, second.PageSize);
        Assert.Empty(beyond.Items);
        Assert.Equal(4, beyond.TotalItems);
    }

    [Fact]
    public async Task Detail_TimelinePaginationIsDeterministicAndSummaryIsNotPaged()
    {
        await using var fixture = await Database.CreateAsync();
        await fixture.SeedAsync(Record(3, 1, From), Record(2, 1, From, "ERROR"),
            Record(1, 1, From), Record(4, 1, From.AddHours(-1)), Record(5, 2, From),
            Evaluation(1, From.AddDays(1), "completed", 4, true));
        var first = await fixture.Service.DetailAsync(Id(1), 1, 2, default);
        var second = await fixture.Service.DetailAsync(Id(1), 2, 2, default);
        var beyond = await fixture.Service.DetailAsync(Id(1), 3, 2, default);

        Assert.Equal(new[] { Id(4), Id(1) }, first.Timeline.Items.Select(x => x.RequestId));
        Assert.Equal(new[] { Id(2), Id(3) }, second.Timeline.Items.Select(x => x.RequestId));
        Assert.Equal(4, second.Timeline.TotalItems);
        Assert.Equal(2, second.Timeline.TotalPages);
        Assert.Equal(2, second.Timeline.Page);
        Assert.Equal(2, second.Timeline.PageSize);
        Assert.Equal(first.Session, second.Session);
        Assert.Equal(4, second.Session.TotalGuidanceRecords);
        Assert.Equal(3, second.Session.SuccessfulRecords);
        Assert.Equal(1, second.Session.FailedRecords);
        Assert.Equal(From.AddHours(-1), second.Session.FirstRecordedAtUtc);
        Assert.Equal(From, second.Session.LastRecordedAtUtc);
        var evaluation = Assert.IsType<AdminEvaluationResponse>(second.Evaluation);
        Assert.Equal(Id(1), evaluation.SessionId);
        Assert.Equal("completed", evaluation.Outcome);
        Assert.Equal(4, evaluation.Rating);
        Assert.True(evaluation.NeededHumanHelp);
        Assert.Equal("manual", evaluation.Source);
        Assert.Equal(From.AddDays(1), evaluation.CreatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, evaluation.CreatedAtUtc.Kind);
        var failure = second.Timeline.Items[0];
        Assert.Equal("failure", failure.Result);
        Assert.Null(failure.Action);
        Assert.Equal("ERROR", failure.ErrorCode);
        Assert.Empty(beyond.Timeline.Items);
        Assert.Equal(4, beyond.Timeline.TotalItems);
    }

    [Fact]
    public async Task Detail_UnevaluatedAndMissingSessions_HaveDistinctResults()
    {
        await using var fixture = await Database.CreateAsync();
        await fixture.SeedAsync(Record(1, 1, From));
        var detail = await fixture.Service.DetailAsync(Id(1), 1, 10, default);
        Assert.Null(detail.Evaluation);
        Assert.False(detail.Session.HasEvaluation);
        Assert.Null(detail.Session.Outcome);
        Assert.Null(detail.Session.Rating);
        var error = await Assert.ThrowsAsync<ApiException>(() => fixture.Service.DetailAsync(Id(2), 1, 10, default));
        Assert.Equal(404, error.StatusCode);
        Assert.Equal("SESSION_NOT_FOUND", error.Code);
    }

    [Theory]
    [InlineData("invalid", null)]
    [InlineData("completed", false)]
    public async Task List_InvalidOutcomeFilters_Return400(string outcome, bool? evaluated)
    {
        await using var fixture = await Database.CreateAsync();
        var error = await Assert.ThrowsAsync<ApiException>(() => fixture.Service.ListAsync(Interval, 1, 10, null, evaluated, outcome, default));
        Assert.Equal(400, error.StatusCode);
        Assert.Equal("INVALID_REQUEST", error.Code);
    }

    private static GuidanceRecord Record(int request, int session, DateTime at, string? error = null) =>
        new(Id(request), Id(session), at, error is null ? "explain" : null, error is null ? "success" : "failure", error);

    private static SessionEvaluation Evaluation(int session, DateTime at, string outcome, int rating, bool help) =>
        new(Id(session), outcome, rating, help, at);

    // Keep the actual Oracle-configured model: SQLite accepts its declared type names,
    // and these tests exercise relational translation rather than EF's InMemory provider.
    private sealed class Database(SqliteConnection connection, GuidanceDbContext db) : IAsyncDisposable
    {
        public AdminQueryService Service { get; } = new(db);

        public static async Task<Database> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new GuidanceDbContext(new DbContextOptionsBuilder<GuidanceDbContext>().UseSqlite(connection).Options);
            try
            {
                await db.Database.EnsureCreatedAsync();
                return new Database(connection, db);
            }
            catch
            {
                await db.DisposeAsync();
                await connection.DisposeAsync();
                throw;
            }
        }

        public async Task SeedAsync(params object[] entities)
        {
            db.AddRange(entities);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }

        public async ValueTask DisposeAsync()
        {
            await db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
