using System.Net;
using System.Text;
using System.Text.Json;
using CitizenTech.Api.Data;
using CitizenTech.Api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CitizenTech.Api.Tests;

public sealed class AdminHttpTests
{
    private const string Key = "http-test-admin-key";
    private const string EvaluationPath = "/api/v1/sessions/11111111-1111-4111-8111-111111111111/evaluation";
    private const string ValidEvaluation = """{"outcome":"completed","rating":4,"neededHumanHelp":false}""";

    [Fact]
    public async Task Health_WithoutKey_PreservesOriginalContract()
    {
        await using var factory = new ApiFactory();
        using var client = factory.Client();
        using var response = await client.GetAsync("/api/v1/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await Body(response);
        Assert.Equal("ok", body.RootElement.GetProperty("status").GetString());
        Assert.Equal("1", body.RootElement.GetProperty("contractVersion").GetString());
        Assert.Equal(2, body.RootElement.EnumerateObject().Count());
        Assert.Equal(0, factory.Store.Calls);
    }

    [Fact]
    public async Task Guidance_InvalidPayloadWithoutKey_PreservesErrorAndClientCorrelation()
    {
        await using var factory = new ApiFactory();
        using var client = factory.Client();
        var requestId = Guid.NewGuid();
        using var response = await client.PostAsync("/api/v1/guidance", Json($$"""{"requestId":"{{requestId}}"}"""));
        await AssertError(response, HttpStatusCode.BadRequest, "INVALID_REQUEST", requestId);
        Assert.Equal(0, factory.Store.Calls);
    }

    [Theory]
    [InlineData("GET", "/api/v1/admin/sessions", null)]
    [InlineData("GET", "/api/v1/admin/sessions", "wrong")]
    [InlineData("GET", "/api/v1/admin/sessions/11111111-1111-4111-8111-111111111111", null)]
    [InlineData("GET", "/api/v1/admin/sessions/11111111-1111-4111-8111-111111111111", "wrong")]
    [InlineData("GET", "/api/v1/admin/metrics", null)]
    [InlineData("GET", "/api/v1/admin/metrics", "wrong")]
    [InlineData("POST", EvaluationPath, null)]
    [InlineData("POST", EvaluationPath, "wrong")]
    public async Task NewEndpoints_MissingOrWrongKey_Return401(string method, string path, string? key)
    {
        await using var factory = new ApiFactory();
        using var client = factory.Client();
        using var response = await Send(client, method, path, key);
        await AssertError(response, HttpStatusCode.Unauthorized, "UNAUTHORIZED");
        Assert.Equal(0, factory.Store.Calls);
    }

    [Theory]
    [InlineData("GET", "/api/v1/admin/sessions")]
    [InlineData("GET", "/api/v1/admin/sessions/11111111-1111-4111-8111-111111111111")]
    [InlineData("GET", "/api/v1/admin/metrics")]
    [InlineData("POST", EvaluationPath)]
    public async Task NewEndpoints_AbsentAdminConfiguration_Return503(string method, string path)
    {
        await using var factory = new ApiFactory(null);
        using var client = factory.Client();
        using var response = await Send(client, method, path, Key);
        await AssertError(response, HttpStatusCode.ServiceUnavailable, "ADMIN_NOT_CONFIGURED");
        Assert.Equal(0, factory.Store.Calls);
    }

    [Fact]
    public async Task Evaluation_ValidRequest_Return201AfterFakeStoreWriteWithoutLocation()
    {
        await using var factory = new ApiFactory();
        using var client = factory.Client();
        using var response = await Send(client, "POST", EvaluationPath, Key);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(response.Headers.Location);
        using var body = await Body(response);
        var evaluation = body.RootElement;
        Assert.NotEqual(Guid.Empty, evaluation.GetProperty("id").GetGuid());
        Assert.Equal(Guid.Parse("11111111-1111-4111-8111-111111111111"), evaluation.GetProperty("sessionId").GetGuid());
        Assert.Equal("completed", evaluation.GetProperty("outcome").GetString());
        Assert.Equal(4, evaluation.GetProperty("rating").GetInt32());
        Assert.False(evaluation.GetProperty("neededHumanHelp").GetBoolean());
        Assert.Equal("manual", evaluation.GetProperty("source").GetString());
        Assert.NotEqual(default, evaluation.GetProperty("createdAtUtc").GetDateTime());
        Assert.NotNull(factory.Store.Added);
        Assert.Equal(3, factory.Store.Calls);
    }

    [Fact]
    public async Task InvalidAdminKeys_AreRateLimitedWithCorrelation_WithoutLimitingHealth()
    {
        await using var factory = new ApiFactory(permitLimit: 1);
        using var client = factory.Client();
        using var first = await client.GetAsync("/api/v1/admin/sessions");
        await AssertError(first, HttpStatusCode.Unauthorized, "UNAUTHORIZED");
        using var second = await client.GetAsync("/api/v1/admin/sessions");
        await AssertError(second, HttpStatusCode.TooManyRequests, "RATE_LIMITED");
        using var health = await client.GetAsync("/api/v1/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal(0, factory.Store.Calls);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{\"outcome\":null,\"rating\":4,\"neededHumanHelp\":false}")]
    [InlineData("{\"outcome\":\"unknown\",\"rating\":4,\"neededHumanHelp\":false}")]
    [InlineData("{\"outcome\":\"completed\",\"rating\":4,\"neededHumanHelp\":false,\"extra\":true}")]
    [InlineData("{}")]
    [InlineData("{\"rating\":4,\"neededHumanHelp\":false}")]
    [InlineData("{\"outcome\":\"completed\",\"neededHumanHelp\":false}")]
    [InlineData("{\"outcome\":\"completed\",\"rating\":4}")]
    public async Task Evaluation_MissingRequiredProperty_Return400WithCorrelation(string payload)
    {
        await using var factory = new ApiFactory();
        using var client = factory.Client();
        using var response = await Send(client, "POST", EvaluationPath, Key, payload);
        await AssertError(response, HttpStatusCode.BadRequest, "INVALID_REQUEST");
        Assert.Equal(0, factory.Store.Calls);
    }

    [Theory]
    [InlineData("POST", "/api/v1/sessions/not-a-uuid/evaluation")]
    [InlineData("POST", "/api/v1/sessions/00000000-0000-0000-0000-000000000000/evaluation")]
    [InlineData("GET", "/api/v1/admin/sessions/not-a-uuid")]
    [InlineData("GET", "/api/v1/admin/sessions/00000000-0000-0000-0000-000000000000")]
    [InlineData("GET", "/api/v1/admin/sessions?page=0")]
    [InlineData("GET", "/api/v1/admin/metrics?from=invalid&to=invalid")]
    public async Task NewEndpoints_InvalidInput_Return400BeforePersistence(string method, string path)
    {
        await using var factory = new ApiFactory();
        using var client = factory.Client();
        using var response = await Send(client, method, path, Key);
        await AssertError(response, HttpStatusCode.BadRequest, "INVALID_REQUEST");
        Assert.Equal(0, factory.Store.Calls);
    }

    [Fact]
    public async Task DevelopmentOpenApi_AdminSecurityAppliesOnlyToNewOperations()
    {
        await using var factory = new ApiFactory();
        using var client = factory.Client();
        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await Body(response);
        var root = body.RootElement;
        var scheme = root.GetProperty("components").GetProperty("securitySchemes").GetProperty("AdminKey");
        Assert.Equal("apiKey", scheme.GetProperty("type").GetString());
        Assert.Equal("header", scheme.GetProperty("in").GetString());
        Assert.Equal("X-Admin-Key", scheme.GetProperty("name").GetString());
        Assert.False(root.TryGetProperty("security", out var global) && global.GetArrayLength() > 0);
        var secured = new HashSet<string>();
        foreach (var path in root.GetProperty("paths").EnumerateObject())
        {
            foreach (var method in new[] { "get", "post", "put", "patch", "delete" })
            {
                if (!path.Value.TryGetProperty(method, out var operation)) continue;
                var isAdmin = path.Name.StartsWith("/api/v1/admin/", StringComparison.Ordinal)
                    || path.Name == "/api/v1/sessions/{sessionId}/evaluation";
                var hasSecurity = operation.TryGetProperty("security", out var security) && security.GetArrayLength() > 0;
                Assert.Equal(isAdmin, hasSecurity);
                if (!isAdmin) continue;
                Assert.True(security[0].TryGetProperty("AdminKey", out _));
                secured.Add($"{method} {path.Name}");
            }
        }
        Assert.Equal(4, secured.Count);
        foreach (var route in new[] { "/api/v1/health", "/api/v1/guidance" })
            Assert.True(root.GetProperty("paths").TryGetProperty(route, out _));
        Assert.Equal(0, factory.Store.Calls);
    }

    private static StringContent Json(string payload) => new(payload, Encoding.UTF8, "application/json");
    private static async Task<HttpResponseMessage> Send(HttpClient client, string method, string path, string? key, string payload = ValidEvaluation)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (key is not null) request.Headers.Add("X-Admin-Key", key);
        if (method == "POST") request.Content = Json(payload);
        return await client.SendAsync(request);
    }

    private static async Task<JsonDocument> Body(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static async Task AssertError(HttpResponseMessage response, HttpStatusCode status, string code, Guid? expectedId = null)
    {
        Assert.Equal(status, response.StatusCode);
        using var body = await Body(response);
        var root = body.RootElement;
        Assert.Equal("1", root.GetProperty("contractVersion").GetString());
        Assert.Equal(code, root.GetProperty("error").GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("error").GetProperty("message").GetString()));
        var correlation = root.GetProperty("requestId").GetGuid();
        Assert.NotEqual(Guid.Empty, correlation);
        if (expectedId.HasValue) Assert.Equal(expectedId.Value, correlation);
    }

    private sealed class ApiFactory(string? key = Key, int permitLimit = 1000) : WebApplicationFactory<Program>
    {
        public FakeEvaluationStore Store { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Admin:ApiKey"] = key,
                    ["Guidance:Provider"] = "Fake",
                    ["RateLimiting:PermitLimit"] = permitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture)
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ISessionEvaluationStore>();
                services.AddSingleton<ISessionEvaluationStore>(Store);
            });
        }

        public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
    }

    private sealed class FakeEvaluationStore : ISessionEvaluationStore
    {
        public int Calls { get; private set; }
        public SessionEvaluation? Added { get; private set; }

        public Task<bool> SessionExistsAsync(Guid sessionId, CancellationToken token)
        {
            Calls++;
            return Task.FromResult(true);
        }

        public Task<bool> EvaluationExistsAsync(Guid sessionId, CancellationToken token)
        {
            Calls++;
            return Task.FromResult(false);
        }

        public Task AddAsync(SessionEvaluation evaluation, CancellationToken token)
        {
            Calls++;
            Added = evaluation;
            return Task.CompletedTask;
        }
    }
}
