using System.Text.Json;
using CitizenTech.Api.Contracts;
using CitizenTech.Api.Data;
using CitizenTech.Api.Errors;
using CitizenTech.Api.Models;
using CitizenTech.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace CitizenTech.Api.Tests;

public sealed class AdminTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"rating\":3,\"neededHumanHelp\":false}")]
    [InlineData("{\"outcome\":\"completed\",\"neededHumanHelp\":false}")]
    [InlineData("{\"outcome\":\"completed\",\"rating\":3}")]
    public void EvaluationJson_MissingRequiredMember_Throws(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<EvaluationRequest>(json, JsonOptions));
    }

    [Theory]
    [InlineData("{\"outcome\":null,\"rating\":3,\"neededHumanHelp\":false}")]
    [InlineData("{\"outcome\":\"completed\",\"rating\":null,\"neededHumanHelp\":false}")]
    [InlineData("{\"outcome\":\"completed\",\"rating\":3,\"neededHumanHelp\":null}")]
    public void EvaluationJson_ExplicitNull_DeserializesButFailsValidation(string json)
    {
        var request = Assert.IsType<EvaluationRequest>(JsonSerializer.Deserialize<EvaluationRequest>(json, JsonOptions));
        AssertError(Assert.Throws<ApiException>(request.Validate), 400, "INVALID_REQUEST");
    }

    [Theory]
    [InlineData("source", "\"manual\"")]
    [InlineData("sessionId", "\"00000000-0000-0000-0000-000000000001\"")]
    [InlineData("unexpected", "null")]
    public void EvaluationJson_ExtraMember_IsForbidden(string member, string value)
    {
        var json = "{\"outcome\":\"completed\",\"rating\":3,\"neededHumanHelp\":false,\"" + member + "\":" + value + "}";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<EvaluationRequest>(json, JsonOptions));
    }

    [Theory]
    [InlineData("completed", 1, false)]
    [InlineData("partially_completed", 3, true)]
    [InlineData("not_completed", 5, false)]
    public void Evaluation_ValidOutcomesAndRatingBounds_AreAccepted(string outcome, int rating, bool help)
    {
        new EvaluationRequest { Outcome = outcome, Rating = rating, NeededHumanHelp = help }.Validate();
    }

    [Theory]
    [InlineData(null, 3)]
    [InlineData("", 3)]
    [InlineData("Completed", 3)]
    [InlineData(" completed", 3)]
    [InlineData("completed ", 3)]
    [InlineData("unknown", 3)]
    [InlineData("completed", null)]
    [InlineData("completed", 0)]
    [InlineData("completed", 6)]
    [InlineData("completed", int.MinValue)]
    [InlineData("completed", int.MaxValue)]
    public void Evaluation_InvalidOutcomeOrRating_IsRejected(string? outcome, int? rating)
    {
        var request = new EvaluationRequest { Outcome = outcome, Rating = rating, NeededHumanHelp = false };
        AssertError(Assert.Throws<ApiException>(request.Validate), 400, "INVALID_REQUEST");
    }

    [Fact]
    public async Task EvaluationService_ValidRequest_PersistsValuesAndControlledUtcTime()
    {
        var store = new StubEvaluationStore();
        using var cancellation = new CancellationTokenSource();
        var sessionId = Guid.NewGuid();
        var result = await Service(store).CreateAsync(sessionId, ValidRequest(), cancellation.Token);

        Assert.Same(store.Added, result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(sessionId, result.SessionId);
        Assert.Equal("completed", result.Outcome);
        Assert.Equal(5, result.Rating);
        Assert.False(result.NeededHumanHelp);
        Assert.Equal("manual", result.Source);
        Assert.Equal(Now.UtcDateTime, result.CreatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, result.CreatedAtUtc.Kind);
        Assert.Equal(new[] { "session", "evaluation", "add" }, store.Calls);
        Assert.All(store.SessionIds, id => Assert.Equal(sessionId, id));
        Assert.All(store.Tokens, token => Assert.Equal(cancellation.Token, token));
    }

    [Fact]
    public async Task EvaluationService_MissingSession_Returns404WithoutDuplicateLookupOrInsert()
    {
        var store = new StubEvaluationStore { SessionExists = false };
        var error = await Assert.ThrowsAsync<ApiException>(() => Service(store).CreateAsync(Guid.NewGuid(), ValidRequest(), default));
        AssertError(error, 404, "SESSION_NOT_FOUND");
        Assert.Equal(new[] { "session" }, store.Calls);
        Assert.Null(store.Added);
    }

    [Fact]
    public async Task EvaluationService_PreviouslyEvaluated_Returns409WithoutInsert()
    {
        var store = new StubEvaluationStore { EvaluationExists = true };
        var error = await Assert.ThrowsAsync<ApiException>(() => Service(store).CreateAsync(Guid.NewGuid(), ValidRequest(), default));
        AssertError(error, 409, "SESSION_ALREADY_EVALUATED");
        Assert.Equal(new[] { "session", "evaluation" }, store.Calls);
        Assert.Null(store.Added);
    }

    [Fact]
    public async Task EvaluationService_DuplicateOnInsert_Returns409AfterNegativeLookup()
    {
        var store = new StubEvaluationStore
        {
            Add = (_, _) => Task.FromException(new DuplicateRecordException(new InvalidOperationException("Duplicate")))
        };
        var error = await Assert.ThrowsAsync<ApiException>(() => Service(store).CreateAsync(Guid.NewGuid(), ValidRequest(), default));
        AssertError(error, 409, "SESSION_ALREADY_EVALUATED");
        Assert.Equal(new[] { "session", "evaluation", "add" }, store.Calls);
    }

    [Theory]
    [InlineData("session")]
    [InlineData("evaluation")]
    [InlineData("add")]
    public async Task EvaluationService_PersistenceFailure_Returns503(string stage)
    {
        var store = new StubEvaluationStore { FailureStage = stage };
        var error = await Assert.ThrowsAsync<ApiException>(() => Service(store).CreateAsync(Guid.NewGuid(), ValidRequest(), default));
        AssertError(error, 503, "PERSISTENCE_UNAVAILABLE");
        Assert.Equal(stage, store.Calls.Last());
    }

    [Fact]
    public async Task EvaluationService_AwaitsPersistenceConfirmation()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var confirmation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new StubEvaluationStore
        {
            Add = (_, _) => { entered.SetResult(); return confirmation.Task; }
        };
        var pending = Service(store).CreateAsync(Guid.NewGuid(), ValidRequest(), default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try { Assert.False(pending.IsCompleted); }
        finally { confirmation.TrySetResult(); }
        Assert.Same(store.Added, await pending.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task EvaluationService_UnconfirmedWriteFailure_DoesNotReturnSuccess()
    {
        var confirmation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new StubEvaluationStore { Add = (_, _) => confirmation.Task };
        var pending = Service(store).CreateAsync(Guid.NewGuid(), ValidRequest(), default);
        Assert.False(pending.IsCompleted);
        confirmation.SetException(new InvalidOperationException("Commit failed"));
        AssertError(await Assert.ThrowsAsync<ApiException>(() => pending), 503, "PERSISTENCE_UNAVAILABLE");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EvaluationService_InvalidInput_DoesNotAccessStore(bool invalidSession)
    {
        var store = new StubEvaluationStore();
        var request = invalidSession ? ValidRequest() : new EvaluationRequest();
        var error = await Assert.ThrowsAsync<ApiException>(() => Service(store).CreateAsync(
            invalidSession ? Guid.Empty : Guid.NewGuid(), request, default));
        AssertError(error, 400, "INVALID_REQUEST");
        Assert.Empty(store.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wrong")]
    [InlineData("secret,secret")]
    [InlineData(" secret")]
    public async Task AdminKey_MissingOrWrongHeader_Returns401(string? header)
    {
        var context = AuthorizationContext();
        if (header is not null) context.HttpContext.Request.Headers["X-Admin-Key"] = header;
        await Filter("secret").OnAuthorizationAsync(context);
        AssertAuthorizationError(context, 401, "UNAUTHORIZED");
    }

    [Fact]
    public async Task AdminKey_MultipleHeaderValues_Returns401EvenIfBothMatch()
    {
        var context = AuthorizationContext();
        context.HttpContext.Request.Headers["X-Admin-Key"] = new StringValues(new[] { "secret", "secret" });
        await Filter("secret").OnAuthorizationAsync(context);
        AssertAuthorizationError(context, 401, "UNAUTHORIZED");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AdminKey_MissingConfiguration_Returns503EvenWithHeader(string? configured)
    {
        var context = AuthorizationContext();
        context.HttpContext.Request.Headers["X-Admin-Key"] = "secret";
        await Filter(configured).OnAuthorizationAsync(context);
        AssertAuthorizationError(context, 503, "ADMIN_NOT_CONFIGURED");
    }

    [Fact]
    public async Task AdminKey_SingleCorrectHeader_AllowsAuthorization()
    {
        var context = AuthorizationContext();
        context.HttpContext.Request.Headers["x-admin-key"] = "secret";
        await Filter("secret").OnAuthorizationAsync(context);
        Assert.Null(context.Result);
    }

    [Fact]
    public void Interval_Default_UsesControlledClockAndThirtyDays()
    {
        var interval = AdminInput.Interval(null, null, new FixedClock(Now.ToOffset(TimeSpan.FromHours(-3))));
        Assert.Equal(Now.UtcDateTime.AddDays(-30), interval.From);
        Assert.Equal(Now.UtcDateTime, interval.To);
        Assert.Equal(DateTimeKind.Utc, interval.From.Kind);
        Assert.Equal(DateTimeKind.Utc, interval.To.Kind);
    }

    [Theory]
    [InlineData("2026-11-11T09:30:00-03:00", "2026-11-11T14:30:00+01:00")]
    [InlineData("2026-11-11T12:30:00Z", "2026-11-11T13:30:00Z")]
    public void Interval_ExplicitTimezone_NormalizesToUtc(string from, string to)
    {
        var interval = AdminInput.Interval(from, to, new FixedClock(Now));
        Assert.Equal(Now.UtcDateTime, interval.From);
        Assert.Equal(Now.UtcDateTime.AddHours(1), interval.To);
        Assert.Equal(DateTimeKind.Utc, interval.From.Kind);
        Assert.Equal(DateTimeKind.Utc, interval.To.Kind);
    }

    [Theory]
    [InlineData(null, "2026-11-12T00:00:00Z")]
    [InlineData("2026-11-11T00:00:00Z", null)]
    [InlineData("2026-11-11", "2026-11-12T00:00:00Z")]
    [InlineData("2026-11-11T00:00:00", "2026-11-12T00:00:00Z")]
    [InlineData("2026-11-11T00:00:00Z", "2026-11-12T00:00:00")]
    [InlineData("2026-02-30T00:00:00Z", "2026-11-12T00:00:00Z")]
    [InlineData("2026-11-11T00:00:00+15:00", "2026-11-12T00:00:00Z")]
    [InlineData("2026-11-11T00:00:00Z", "2026-11-11T00:00:00Z")]
    [InlineData("2026-11-12T00:00:00Z", "2026-11-11T00:00:00Z")]
    [InlineData("2026-11-11T09:00:00-03:00", "2026-11-11T12:00:00Z")]
    [InlineData("", "2026-11-12T00:00:00Z")]
    public void Interval_InvalidOrNonIncreasingRange_Returns400(string? from, string? to)
    {
        AssertError(Assert.Throws<ApiException>(() => AdminInput.Interval(from, to, new FixedClock(Now))), 400, "INVALID_REQUEST");
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 100)]
    [InlineData(int.MaxValue, 1)]
    [InlineData(21474837, 100)]
    public void Pagination_ValidBounds_AreAccepted(int page, int size) => AdminInput.Pagination(page, size);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    [InlineData(1, 101)]
    [InlineData(21474838, 100)]
    [InlineData(int.MaxValue, 100)]
    public void Pagination_InvalidBoundsOrOffsetOverflow_Returns400(int page, int size)
    {
        AssertError(Assert.Throws<ApiException>(() => AdminInput.Pagination(page, size)), 400, "INVALID_REQUEST");
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void SessionId_InvalidValue_Returns400(string value)
    {
        AssertError(Assert.Throws<ApiException>(() => AdminInput.SessionId(value)), 400, "INVALID_REQUEST");
    }

    [Fact]
    public void SessionId_ValidValue_IsParsed()
    {
        var id = Guid.NewGuid();
        Assert.Equal(id, AdminInput.SessionId(id.ToString()));
    }

    private static EvaluationRequest ValidRequest() => new() { Outcome = "completed", Rating = 5, NeededHumanHelp = false };
    private static EvaluationService Service(StubEvaluationStore store) => new(store, new FixedClock(Now));
    private static void AssertError(ApiException error, int status, string code)
    {
        Assert.Equal(status, error.StatusCode);
        Assert.Equal(code, error.Code);
    }

    private static AdminKeyFilter Filter(string? key) => new(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Admin:ApiKey"] = key }).Build());

    private static AuthorizationFilterContext AuthorizationContext()
    {
        var http = new DefaultHttpContext();
        http.Items["RequestId"] = Guid.NewGuid();
        return new(new ActionContext(http, new RouteData(), new ActionDescriptor()), new List<IFilterMetadata>());
    }

    private static void AssertAuthorizationError(AuthorizationFilterContext context, int status, string code)
    {
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(status, result.StatusCode);
        var body = Assert.IsType<ErrorResponse>(result.Value);
        Assert.Equal(code, body.Error.Code);
        Assert.Equal((Guid)context.HttpContext.Items["RequestId"]!, body.RequestId);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StubEvaluationStore : ISessionEvaluationStore
    {
        public bool SessionExists { get; init; } = true;
        public bool EvaluationExists { get; init; }
        public string? FailureStage { get; init; }
        public Func<SessionEvaluation, CancellationToken, Task> Add { get; init; } = (_, _) => Task.CompletedTask;
        public List<string> Calls { get; } = [];
        public List<Guid> SessionIds { get; } = [];
        public List<CancellationToken> Tokens { get; } = [];
        public SessionEvaluation? Added { get; private set; }

        public Task<bool> SessionExistsAsync(Guid sessionId, CancellationToken token)
        {
            Record("session", sessionId, token);
            return Task.FromResult(SessionExists);
        }

        public Task<bool> EvaluationExistsAsync(Guid sessionId, CancellationToken token)
        {
            Record("evaluation", sessionId, token);
            return Task.FromResult(EvaluationExists);
        }

        public Task AddAsync(SessionEvaluation evaluation, CancellationToken token)
        {
            Record("add", evaluation.SessionId, token);
            Added = evaluation;
            return Add(evaluation, token);
        }

        private void Record(string stage, Guid sessionId, CancellationToken token)
        {
            Calls.Add(stage);
            SessionIds.Add(sessionId);
            Tokens.Add(token);
            if (FailureStage == stage) throw new InvalidOperationException("Persistence unavailable");
        }
    }
}
