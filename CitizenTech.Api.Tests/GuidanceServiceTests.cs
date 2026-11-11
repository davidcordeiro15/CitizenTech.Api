using CitizenTech.Api.Contracts;
using CitizenTech.Api.Data;
using CitizenTech.Api.Errors;
using CitizenTech.Api.Models;
using CitizenTech.Api.Services;

namespace CitizenTech.Api.Tests;

public sealed class GuidanceServiceTests
{
    [Fact]
    public async Task InvalidRequest_DoesNotCallProviderOrStore()
    {
        var provider = new StubProvider();
        var store = new StubStore();
        var service = new GuidanceService(provider, store);

        var error = await Assert.ThrowsAsync<ApiException>(() => service.GuideAsync(new GuidanceRequest(), default));

        AssertError(error, 400, "INVALID_REQUEST");
        Assert.Equal(0, provider.Calls);
        Assert.Equal(0, store.ExistsCalls);
        Assert.Equal(0, store.AddCalls);
    }

    [Fact]
    public async Task FakeProvider_UsesActualEnabledTarget_NotHardcodedE1()
    {
        var request = ValidRequest([
            Element("e1", "Outro botão"),
            Element("actual-schedule-button", "Agendamento")
        ]);

        var decision = await new FakeGuidanceProvider().GenerateAsync(request, default);

        Assert.Equal("highlight", decision.Action);
        Assert.Equal("actual-schedule-button", decision.TargetElementId);
        Assert.True(decision.IsValidFor(request));
    }

    [Fact]
    public async Task FakeProvider_DisabledAgendamento_ReturnsExplainWithoutTarget()
    {
        var request = ValidRequest([Element("disabled-target", "Agendamento", enabled: false)]);

        var decision = await new FakeGuidanceProvider().GenerateAsync(request, default);

        Assert.Equal("explain", decision.Action);
        Assert.Null(decision.TargetElementId);
        Assert.True(decision.IsValidFor(request));
    }

    [Theory]
    [InlineData("missing-target")]
    [InlineData("disabled-target")]
    public async Task InvalidDecisionTarget_IsRejectedWithoutPersisting(string target)
    {
        var provider = new StubProvider { Decision = new("highlight", "Toque aqui.", target) };
        var store = new StubStore();
        var request = ValidRequest([
            Element("actual-schedule-button", "Agendamento"),
            Element("disabled-target", "Agendamento", enabled: false)
        ]);

        var error = await Assert.ThrowsAsync<ApiException>(() => new GuidanceService(provider, store).GuideAsync(request, default));

        AssertError(error, 502, "AI_UNAVAILABLE");
        Assert.Equal(1, provider.Calls);
        Assert.Equal(0, store.AddCalls);
    }

    [Fact]
    public async Task Success_IsReturnedOnlyAfterAddAsyncIsConfirmed()
    {
        var enteredAdd = new TaskCompletionSource<GuidanceRecord>(TaskCreationOptions.RunContinuationsAsynchronously);
        var confirmed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new StubStore
        {
            Add = (record, _) =>
            {
                enteredAdd.SetResult(record);
                return confirmed.Task;
            }
        };
        var provider = new StubProvider();
        var request = ValidRequest();
        var before = DateTime.UtcNow;
        var pending = new GuidanceService(provider, store).GuideAsync(request, default);

        var record = await enteredAdd.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            Assert.False(pending.IsCompleted);
            Assert.Equal(request.RequestId, record.RequestId);
            Assert.Equal(request.SessionId, record.SessionId);
            Assert.Equal("success", record.Result);
            Assert.Equal(provider.Decision.Action, record.Action);
            Assert.Null(record.ErrorCode);
            Assert.Equal(DateTimeKind.Utc, record.CreatedAtUtc.Kind);
            Assert.InRange(record.CreatedAtUtc, before, DateTime.UtcNow);
        }
        finally
        {
            confirmed.TrySetResult();
        }

        var response = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("1", response.ContractVersion);
        Assert.Equal(request.RequestId, response.RequestId);
        Assert.Equal(request.SessionId, response.SessionId);
        Assert.Equal(request.Screen!.SnapshotId, response.SnapshotId);
        Assert.Equal(provider.Decision.Action, response.Action);
        Assert.Equal(provider.Decision.Instruction, response.Instruction);
        Assert.Equal(provider.Decision.TargetElementId, response.TargetElementId);
        Assert.Equal(1, store.AddCalls);
    }

    [Fact]
    public async Task FailedPersistence_DoesNotReturnSuccess()
    {
        var enteredAdd = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var confirmation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new StubProvider();
        var store = new StubStore
        {
            Add = (_, _) =>
            {
                enteredAdd.SetResult();
                return confirmation.Task;
            }
        };
        var pending = new GuidanceService(provider, store).GuideAsync(ValidRequest(), default);
        await enteredAdd.Task.WaitAsync(TimeSpan.FromSeconds(5));
        confirmation.SetException(new PersistenceException(new InvalidOperationException("Commit not confirmed")));

        var error = await Assert.ThrowsAsync<ApiException>(() => pending);

        AssertError(error, 503, "PERSISTENCE_UNAVAILABLE");
        Assert.Equal(1, provider.Calls);
        Assert.Equal(1, store.AddCalls);
    }

    [Fact]
    public async Task ExistingRequest_RejectsBeforeCallingProvider()
    {
        var provider = new StubProvider();
        var store = new StubStore { Exists = (_, _) => Task.FromResult(true) };
        var request = ValidRequest();

        var error = await Assert.ThrowsAsync<ApiException>(() => new GuidanceService(provider, store).GuideAsync(request, default));

        AssertError(error, 409, "REQUEST_ALREADY_RECORDED");
        Assert.Equal(request.RequestId, store.CheckedRequestId);
        Assert.Equal(1, store.ExistsCalls);
        Assert.Equal(0, provider.Calls);
        Assert.Equal(0, store.AddCalls);
    }

    [Fact]
    public async Task InsertConflict_AfterNegativeLookup_ReturnsConflict()
    {
        var provider = new StubProvider();
        var store = new StubStore
        {
            Add = (_, _) => Task.FromException(new DuplicateRecordException(new InvalidOperationException("Unique constraint")))
        };

        var error = await Assert.ThrowsAsync<ApiException>(() => new GuidanceService(provider, store).GuideAsync(ValidRequest(), default));

        AssertError(error, 409, "REQUEST_ALREADY_RECORDED");
        Assert.Equal(1, store.ExistsCalls);
        Assert.Equal(1, provider.Calls);
        Assert.Equal(1, store.AddCalls);
    }

    [Fact]
    public async Task FailedDuplicateLookup_DoesNotCallProviderOrInsert()
    {
        var provider = new StubProvider();
        var store = new StubStore
        {
            Exists = (_, _) => Task.FromException<bool>(new PersistenceException(new InvalidOperationException("Unavailable")))
        };

        var error = await Assert.ThrowsAsync<ApiException>(() => new GuidanceService(provider, store).GuideAsync(ValidRequest(), default));

        AssertError(error, 503, "PERSISTENCE_UNAVAILABLE");
        Assert.Equal(0, provider.Calls);
        Assert.Equal(0, store.AddCalls);
    }

    [Fact]
    public async Task ExhaustedTotalBudget_DoesNotStartWork()
    {
        var provider = new StubProvider();
        var store = new StubStore();

        var error = await Assert.ThrowsAsync<ApiException>(() => new GuidanceService(provider, store)
            .GuideAsync(ValidRequest(), default, TimeSpan.FromSeconds(20)));

        AssertError(error, 503, "PERSISTENCE_UNAVAILABLE");
        Assert.Equal(0, store.ExistsCalls);
        Assert.Equal(0, provider.Calls);
        Assert.Equal(0, store.AddCalls);
    }

    private static void AssertError(ApiException error, int status, string code)
    {
        Assert.Equal(status, error.StatusCode);
        Assert.Equal(code, error.Code);
    }

    internal static GuidanceRequest ValidRequest(List<ScreenElement?>? elements = null) => new()
    {
        ContractVersion = "1",
        RequestId = Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        Locale = "pt-BR",
        Goal = "Agendar atendimento",
        UserMessage = null,
        History = [],
        Screen = new()
        {
            SnapshotId = Guid.NewGuid(),
            PackageName = "br.gov.demo",
            Elements = elements ?? [Element("actual-schedule-button", "Agendamento")]
        }
    };

    private static ScreenElement Element(string id, string text, bool enabled = true) => new()
    {
        Id = id, Text = text, Role = "button", Enabled = enabled
    };

    private sealed class StubProvider : IGuidanceProvider
    {
        public int Calls { get; private set; }
        public GuidanceDecision Decision { get; init; } = new("highlight", "Toque em Agendamento.", "actual-schedule-button");

        public Task<GuidanceDecision> GenerateAsync(GuidanceRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Decision);
        }
    }

    private sealed class StubStore : IGuidanceRecordStore
    {
        public int ExistsCalls { get; private set; }
        public int AddCalls { get; private set; }
        public Guid? CheckedRequestId { get; private set; }
        public Func<Guid, CancellationToken, Task<bool>> Exists { get; init; } = (_, _) => Task.FromResult(false);
        public Func<GuidanceRecord, CancellationToken, Task> Add { get; init; } = (_, _) => Task.CompletedTask;

        public Task<bool> ExistsAsync(Guid requestId, CancellationToken cancellationToken)
        {
            ExistsCalls++;
            CheckedRequestId = requestId;
            return Exists(requestId, cancellationToken);
        }

        public Task AddAsync(GuidanceRecord record, CancellationToken cancellationToken)
        {
            AddCalls++;
            return Add(record, cancellationToken);
        }
    }
}
