using CitizenTech.Api.Contracts;
using CitizenTech.Api.Errors;

namespace CitizenTech.Api.Services;

public static class GuidanceRegistration
{
    public static IServiceCollection AddGuidanceProviders(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GeminiOptions>(configuration.GetSection("Gemini"));
        services.AddHttpClient(GeminiGuidanceProvider.ClientName, client =>
        {
            // O timeout pertence ao serviço e inclui leitura/deserialização da resposta.
            client.Timeout = Timeout.InfiniteTimeSpan;
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AllowAutoRedirect = false // Não encaminha a chave para outro endereço nem repete chamadas.
        }).RedactLoggedHeaders(["x-goog-api-key"]).RemoveAllLoggers();
        services.AddTransient<GeminiGuidanceProvider>();
        switch (configuration["Guidance:Provider"])
        {
            case "Fake": services.AddSingleton<IGuidanceProvider, FakeGuidanceProvider>(); break;
            case "Gemini": services.AddTransient<IGuidanceProvider, GeminiGuidanceProvider>(); break;
            default: services.AddSingleton<IGuidanceProvider, UnconfiguredGuidanceProvider>(); break;
        }
        return services;
    }

    // Seleção ausente/inválida não ativa Fake e não compromete o health.
    private sealed class UnconfiguredGuidanceProvider : IGuidanceProvider
    {
        public Task<GuidanceDecision> GenerateAsync(GuidanceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new ApiException(502, "AI_UNAVAILABLE");
        }
    }
}
