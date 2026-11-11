using System.Net;
using System.Text;
using System.Text.Json;
using CitizenTech.Api.Data;
using CitizenTech.Api.Errors;
using CitizenTech.Api.Models;
using CitizenTech.Api.Services;
using Microsoft.Extensions.Options;

namespace CitizenTech.Api.Tests;

public sealed class GeminiGuidanceProviderTests
{
    private const string ApiKey = "test-secret-not-for-url";
    private const string ValidDecision = """{"action":"highlight","instruction":"Toque em Agendamento.","targetElementId":"actual-schedule-button"}""";

    [Fact]
    public async Task ValidEnvelope_ReturnsDecision_AndSendsOnlyAllowedContextWithSchema()
    {
        var request = GuidanceServiceTests.ValidRequest();
        var factory = new StubClientFactory(Envelope(ValidDecision));

        var decision = await Provider(factory).GenerateAsync(request, default);

        Assert.Equal("highlight", decision.Action);
        Assert.Equal("Toque em Agendamento.", decision.Instruction);
        Assert.Equal("actual-schedule-button", decision.TargetElementId);
        Assert.Equal(1, factory.Calls);
        Assert.Equal("Gemini", factory.ClientName);
        Assert.Equal(HttpMethod.Post, factory.Method);
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/gemini-test:generateContent", factory.Url);
        Assert.DoesNotContain(ApiKey, factory.Url!);
        Assert.Equal(ApiKey, factory.Key);
        using var payload = JsonDocument.Parse(factory.Body!);
        var root = payload.RootElement;
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString()));
        var contents = root.GetProperty("contents");
        Assert.Equal(1, contents.GetArrayLength());
        Assert.Equal("user", contents[0].GetProperty("role").GetString());
        using var context = JsonDocument.Parse(contents[0].GetProperty("parts")[0].GetProperty("text").GetString()!);
        Assert.Equal(new[] { "elements", "goal", "history", "userMessage" }, context.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
        Assert.Equal(request.Goal, context.RootElement.GetProperty("goal").GetString());
        Assert.Equal(JsonValueKind.Null, context.RootElement.GetProperty("userMessage").ValueKind);
        Assert.Equal(0, context.RootElement.GetProperty("history").GetArrayLength());
        var element = context.RootElement.GetProperty("elements")[0];
        Assert.Equal(new[] { "enabled", "id", "role", "text" }, element.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
        Assert.Equal("actual-schedule-button", element.GetProperty("id").GetString());
        Assert.Equal("button", element.GetProperty("role").GetString());
        Assert.Equal("Agendamento", element.GetProperty("text").GetString());
        Assert.True(element.GetProperty("enabled").GetBoolean());
        Assert.DoesNotContain(request.RequestId.ToString(), factory.Body!);
        Assert.DoesNotContain(request.SessionId.ToString(), factory.Body!);
        Assert.DoesNotContain(request.Screen!.SnapshotId.ToString(), factory.Body!);
        Assert.DoesNotContain(request.Screen.PackageName!, factory.Body!);
        Assert.DoesNotContain("packageName", factory.Body!);
        var config = root.GetProperty("generationConfig");
        Assert.Equal("application/json", config.GetProperty("responseMimeType").GetString());
        Assert.Equal(1, config.GetProperty("candidateCount").GetInt32());
        var schema = config.GetProperty("responseJsonSchema");
        Assert.Equal("object", schema.GetProperty("type").GetString());
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(new[] { "action", "instruction", "targetElementId" }, schema.GetProperty("required").EnumerateArray().Select(v => v.GetString()).ToArray());
        var properties = schema.GetProperty("properties");
        Assert.Equal(3, properties.EnumerateObject().Count());
        Assert.Equal(new[] { "highlight", "explain", "ask", "complete" }, properties.GetProperty("action").GetProperty("enum").EnumerateArray().Select(v => v.GetString()).ToArray());
        Assert.Equal(1, properties.GetProperty("instruction").GetProperty("minLength").GetInt32());
        Assert.Equal(250, properties.GetProperty("instruction").GetProperty("maxLength").GetInt32());
        Assert.Equal(new[] { "string", "null" }, properties.GetProperty("targetElementId").GetProperty("type").EnumerateArray().Select(v => v.GetString()).ToArray());
    }

    [Theory]
    [InlineData("missing-target")]
    [InlineData("disabled-target")]
    public async Task InvalidTarget_IsRejectedByService_WithoutAddingRecord(string target)
    {
        var request = GuidanceServiceTests.ValidRequest([
            new() { Id = "actual-schedule-button", Text = "Agendamento", Role = "button", Enabled = true },
            new() { Id = "disabled-target", Text = "Agendamento", Role = "button", Enabled = false }
        ]);
        var factory = new StubClientFactory(Envelope(JsonSerializer.Serialize(new { action = "highlight", instruction = "Toque aqui.", targetElementId = target })));
        var store = new StubStore();

        var error = await Assert.ThrowsAsync<ApiException>(() => new GuidanceService(Provider(factory), store).GuideAsync(request, default));

        AssertError(error, 502, "AI_UNAVAILABLE");
        Assert.Equal(1, factory.Calls);
        Assert.Equal(0, store.AddCalls);
    }

    public static IEnumerable<object[]> InvalidResponses()
    {
        yield return ["not-json"];
        yield return [""];
        yield return ["{}"];
        yield return ["{\"candidates\":[]}"];
        yield return ["{\"promptFeedback\":{\"blockReason\":\"SAFETY\"}}"];
        yield return [Envelope("not-json")];
        yield return [Envelope("")];
        yield return [Envelope("null")];
        yield return [Envelope("{}")];
        yield return [Envelope(ValidDecision, "MAX_TOKENS")];
        yield return [Envelope(ValidDecision, "SAFETY")];
        yield return ["{\"candidates\":[{\"finishReason\":\"STOP\",\"safetyRatings\":[{\"blocked\":true}]}]}"];
        yield return ["{\"candidates\":[{\"finishReason\":\"STOP\",\"content\":{\"parts\":[]}}]}"];
    }

    [Theory]
    [MemberData(nameof(InvalidResponses))]
    public async Task InvalidOrBlockedResponse_Returns502WithoutFallbackOrPersistence(string body)
    {
        var factory = new StubClientFactory(body);
        var store = new StubStore();
        // The screen contains Agendamento, so a fallback to Fake would incorrectly succeed.
        var error = await Assert.ThrowsAsync<ApiException>(() => new GuidanceService(Provider(factory), store)
            .GuideAsync(GuidanceServiceTests.ValidRequest(), default));

        AssertError(error, 502, "AI_UNAVAILABLE");
        Assert.Equal(1, factory.Calls);
        Assert.Equal(0, store.AddCalls);
        Assert.DoesNotContain(ApiKey, error.Message);
        Assert.DoesNotContain(body.Length > 0 ? body : "private-response", error.Message);
    }

    [Theory]
    [InlineData(429)]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task HttpErrors_Return502WithoutRetryFallbackOrPersistence(int status)
    {
        var factory = new StubClientFactory("private-google-error", (HttpStatusCode)status);
        var store = new StubStore();

        var error = await Assert.ThrowsAsync<ApiException>(() => new GuidanceService(Provider(factory), store)
            .GuideAsync(GuidanceServiceTests.ValidRequest(), default));

        AssertError(error, 502, "AI_UNAVAILABLE");
        Assert.Equal(1, factory.Calls);
        Assert.Equal(0, store.AddCalls);
        Assert.DoesNotContain("private-google-error", error.Message);
        Assert.DoesNotContain(ApiKey, error.Message);
    }

    [Fact]
    public async Task TransportFailure_Returns502WithoutPersistence()
    {
        var factory = new StubClientFactory("") { Send = _ => throw new HttpRequestException("private transport detail") };
        var store = new StubStore();

        var error = await Assert.ThrowsAsync<ApiException>(() => new GuidanceService(Provider(factory), store)
            .GuideAsync(GuidanceServiceTests.ValidRequest(), default));

        AssertError(error, 502, "AI_UNAVAILABLE");
        Assert.Equal(1, factory.Calls);
        Assert.Equal(0, store.AddCalls);
        Assert.DoesNotContain("private transport detail", error.Message);
    }

    [Fact]
    public async Task HandlerTimeout_PropagatesFromProvider_AndServiceConvertsTo504()
    {
        var factory = new StubClientFactory("") { Send = _ => throw new OperationCanceledException("timeout") };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Provider(factory).GenerateAsync(GuidanceServiceTests.ValidRequest(), default));
        var store = new StubStore();

        var error = await Assert.ThrowsAsync<ApiException>(() => new GuidanceService(Provider(factory), store)
            .GuideAsync(GuidanceServiceTests.ValidRequest(), default));

        AssertError(error, 504, "AI_TIMEOUT");
        Assert.Equal(2, factory.Calls);
        Assert.Equal(0, store.AddCalls);
    }

    [Fact]
    public async Task ClientCancellationDuringSend_PropagatesWithoutPersistence()
    {
        using var cancellation = new CancellationTokenSource();
        var factory = new StubClientFactory("")
        {
            Send = token =>
            {
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
                throw new InvalidOperationException("Cancellation should have propagated");
            }
        };
        var store = new StubStore();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new GuidanceService(Provider(factory), store)
            .GuideAsync(GuidanceServiceTests.ValidRequest(), cancellation.Token));

        Assert.Equal(1, factory.Calls);
        Assert.Equal(0, store.AddCalls);
    }

    [Fact]
    public async Task AlreadyCancelledClient_DoesNotSendRequest()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var factory = new StubClientFactory(Envelope(ValidDecision));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Provider(factory)
            .GenerateAsync(GuidanceServiceTests.ValidRequest(), cancellation.Token));

        Assert.Equal(0, factory.Calls);
        Assert.Null(factory.ClientName);
    }

    [Theory]
    [InlineData(null, "gemini-test")]
    [InlineData("", "gemini-test")]
    [InlineData("  ", "gemini-test")]
    [InlineData(ApiKey, null)]
    [InlineData(ApiKey, "")]
    [InlineData(ApiKey, "  ")]
    [InlineData(ApiKey, "invalid/model")]
    public async Task MissingOrInvalidConfiguration_Returns502WithoutCreatingClient(string? key, string? model)
    {
        var factory = new StubClientFactory(Envelope(ValidDecision));
        var provider = new GeminiGuidanceProvider(factory, Options.Create(new GeminiOptions { ApiKey = key, Model = model }));
        var store = new StubStore();

        var error = await Assert.ThrowsAsync<ApiException>(() => new GuidanceService(provider, store)
            .GuideAsync(GuidanceServiceTests.ValidRequest(), default));

        AssertError(error, 502, "AI_UNAVAILABLE");
        Assert.Equal(0, factory.Calls);
        Assert.Null(factory.ClientName);
        Assert.Equal(0, store.AddCalls);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(19)]
    public async Task ReservedPersistenceBudget_DoesNotStartGeminiWhenOnlyFiveSecondsOrLessRemain(int elapsedSeconds)
    {
        var factory = new StubClientFactory(Envelope(ValidDecision));
        var store = new StubStore();

        var error = await Assert.ThrowsAsync<ApiException>(() => new GuidanceService(Provider(factory), store)
            .GuideAsync(GuidanceServiceTests.ValidRequest(), default, TimeSpan.FromSeconds(elapsedSeconds)));

        AssertError(error, 504, "AI_TIMEOUT");
        Assert.Equal(1, store.ExistsCalls);
        Assert.Equal(0, factory.Calls);
        Assert.Null(factory.ClientName);
        Assert.Equal(0, store.AddCalls);
    }

    private static GeminiGuidanceProvider Provider(StubClientFactory factory) =>
        new(factory, Options.Create(new GeminiOptions { ApiKey = ApiKey, Model = "gemini-test" }));

    private static string Envelope(string text, string finishReason = "STOP") => JsonSerializer.Serialize(new
    {
        candidates = new[] { new { content = new { parts = new[] { new { text } } }, finishReason } }
    });

    private static void AssertError(ApiException error, int status, string code)
    {
        Assert.Equal(status, error.StatusCode);
        Assert.Equal(code, error.Code);
    }

    private sealed class StubStore : IGuidanceRecordStore
    {
        public int AddCalls { get; private set; }
        public int ExistsCalls { get; private set; }
        public Task<bool> ExistsAsync(Guid requestId, CancellationToken cancellationToken)
        {
            ExistsCalls++;
            return Task.FromResult(false);
        }
        public Task AddAsync(GuidanceRecord record, CancellationToken cancellationToken)
        {
            AddCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class StubClientFactory(string body, HttpStatusCode status = HttpStatusCode.OK) : IHttpClientFactory
    {
        public int Calls { get; private set; }
        public string? ClientName { get; private set; }
        public string? Url { get; private set; }
        public HttpMethod? Method { get; private set; }
        public string? Key { get; private set; }
        public string? Body { get; private set; }
        public Func<CancellationToken, HttpResponseMessage>? Send { get; init; }

        public HttpClient CreateClient(string name)
        {
            ClientName = name;
            return new HttpClient(new StubHandler(async (request, token) =>
            {
                Calls++;
                Url = request.RequestUri!.AbsoluteUri;
                Method = request.Method;
                Key = request.Headers.GetValues("x-goog-api-key").Single();
                Body = await request.Content!.ReadAsStringAsync(token);
                return Send is not null ? Send(token) : new HttpResponseMessage(status)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
            }));
        }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
