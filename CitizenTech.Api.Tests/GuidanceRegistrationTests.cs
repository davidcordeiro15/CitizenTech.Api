using CitizenTech.Api.Contracts;
using CitizenTech.Api.Controllers;
using CitizenTech.Api.Errors;
using CitizenTech.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CitizenTech.Api.Tests;

public sealed class GuidanceRegistrationTests
{
    [Fact]
    public async Task ExplicitFake_ResolvesFakeAndGeneratesWithoutHttp()
    {
        var services = new ServiceCollection();
        var factory = new NoNetworkClientFactory();
        var returned = services.AddGuidanceProviders(Configuration("Fake"));
        services.AddSingleton<IHttpClientFactory>(factory);
        using var container = services.BuildServiceProvider();

        Assert.Same(services, returned);
        var provider = Assert.IsType<FakeGuidanceProvider>(container.GetRequiredService<IGuidanceProvider>());
        var request = GuidanceServiceTests.ValidRequest();
        var decision = await provider.GenerateAsync(request, default);

        Assert.True(decision.IsValidFor(request));
        Assert.Equal("highlight", decision.Action);
        Assert.Equal("actual-schedule-button", decision.TargetElementId);
        Assert.Equal(0, factory.Calls);
    }

    [Fact]
    public void ExplicitGemini_ResolvesGeminiAndBindsOptions()
    {
        var services = new ServiceCollection();
        services.AddGuidanceProviders(Configuration("Gemini", "test-key", "gemini-test"));
        using var container = services.BuildServiceProvider();

        Assert.IsType<GeminiGuidanceProvider>(container.GetRequiredService<IGuidanceProvider>());
        var settings = container.GetRequiredService<Microsoft.Extensions.Options.IOptions<GeminiOptions>>().Value;
        Assert.Equal("test-key", settings.ApiKey);
        Assert.Equal("gemini-test", settings.Model);
        // Creating a named HttpClient does not send a request.
        using var client = container.GetRequiredService<IHttpClientFactory>().CreateClient("Gemini");
        Assert.Equal(Timeout.InfiniteTimeSpan, client.Timeout);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Unknown")]
    [InlineData("fake")]
    [InlineData("gemini")]
    public async Task MissingOrInvalidSelection_DoesNotDefaultToFake(string? selection)
    {
        var services = new ServiceCollection();
        var factory = new NoNetworkClientFactory();
        services.AddGuidanceProviders(Configuration(selection, "test-key", "gemini-test"));
        services.AddSingleton<IHttpClientFactory>(factory);
        using var container = services.BuildServiceProvider();
        var provider = container.GetRequiredService<IGuidanceProvider>();

        Assert.IsNotType<FakeGuidanceProvider>(provider);
        Assert.IsNotType<GeminiGuidanceProvider>(provider);
        var error = await Assert.ThrowsAsync<ApiException>(() => provider.GenerateAsync(GuidanceServiceTests.ValidRequest(), default));

        Assert.Equal(502, error.StatusCode);
        Assert.Equal("AI_UNAVAILABLE", error.Code);
        Assert.Equal(0, factory.Calls);
    }

    [Fact]
    public async Task GeminiWithoutKey_ResolvesButReturns502WithoutCreatingHttpClient()
    {
        var services = new ServiceCollection();
        var factory = new NoNetworkClientFactory();
        services.AddGuidanceProviders(Configuration("Gemini", model: "gemini-test"));
        services.AddSingleton<IHttpClientFactory>(factory);
        using var container = services.BuildServiceProvider();
        var provider = Assert.IsType<GeminiGuidanceProvider>(container.GetRequiredService<IGuidanceProvider>());

        var error = await Assert.ThrowsAsync<ApiException>(() => provider.GenerateAsync(GuidanceServiceTests.ValidRequest(), default));

        Assert.Equal(502, error.StatusCode);
        Assert.Equal("AI_UNAVAILABLE", error.Code);
        Assert.Equal(0, factory.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Unknown")]
    [InlineData("Gemini")]
    public void Health_RemainsAvailableWithMissingAiConfiguration(string? selection)
    {
        var services = new ServiceCollection();
        var factory = new NoNetworkClientFactory();
        services.AddGuidanceProviders(Configuration(selection));
        services.AddSingleton<IHttpClientFactory>(factory);
        services.AddTransient<HealthController>();
        using var container = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        var result = container.GetRequiredService<HealthController>().Get();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(200, ok.StatusCode);
        var response = Assert.IsType<HealthResponse>(ok.Value);
        Assert.Equal("ok", response.Status);
        Assert.Equal("1", response.ContractVersion);
        Assert.Equal(0, factory.Calls);
    }

    private static IConfiguration Configuration(string? selection, string? key = null, string? model = null) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Guidance:Provider"] = selection,
            ["Gemini:ApiKey"] = key,
            ["Gemini:Model"] = model
        }).Build();

    private sealed class NoNetworkClientFactory : IHttpClientFactory
    {
        public int Calls { get; private set; }
        public HttpClient CreateClient(string name)
        {
            Calls++;
            throw new InvalidOperationException("HTTP must not be used in this test.");
        }
    }
}
