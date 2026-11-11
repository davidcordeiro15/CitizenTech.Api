using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CitizenTech.Api.Errors;

public sealed class ContractOpenApiTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info.Title = "CitizenTech API — orientação Fake ou Gemini, persistência Oracle";
        document.Info.Description = "Contrato v1, revisão 1.1. Sem repetição automática. Até 64 KiB, 80 elementos e 3 orientações anteriores. Prazo total 20 s (até 15 s de geração). HTTP 200 confirma metadados no Oracle, não conclusão da tarefa. Novas tentativas exigem novo requestId e snapshotId.";
        if (document.Paths.TryGetValue("/api/v1/health", out var health))
            health.Description = "Somente disponibilidade da aplicação: {\"status\":\"ok\",\"contractVersion\":\"1\"}. Não consulta dependências.";
        if (document.Paths.TryGetValue("/api/v1/guidance", out var path) && path.Operations is not null)
        {
            foreach (var operation in path.Operations.Values)
            {
                if (operation.RequestBody?.Content?.TryGetValue("application/json", out var media) == true)
                    media.Example = JsonNode.Parse("""
                    {"contractVersion":"1","requestId":"a1111111-1111-4111-8111-111111111111","sessionId":"b2222222-2222-4222-8222-222222222222","locale":"pt-BR","goal":"Quero agendar a renovação do meu RG","userMessage":null,"history":[],"screen":{"snapshotId":"c3333333-3333-4333-8333-333333333333","packageName":"com.android.chrome","elements":[{"id":"botao-real","role":"button","text":"Agendamento","enabled":true}]}}
                    """);
                foreach (var (status, response) in operation.Responses ?? [])
                {
                    var code = status switch
                    {
                        "400" => "INVALID_REQUEST", "409" => "REQUEST_ALREADY_RECORDED", "413" => "REQUEST_TOO_LARGE",
                        "429" => "RATE_LIMITED", "502" => "AI_UNAVAILABLE", "503" => "PERSISTENCE_UNAVAILABLE",
                        "504" => "AI_TIMEOUT", "500" => "INTERNAL_ERROR", _ => null
                    };
                    if (response.Content is null) continue;
                    foreach (var content in response.Content.Values)
                        content.Example = code is null ? JsonNode.Parse("""
                        {"contractVersion":"1","requestId":"a1111111-1111-4111-8111-111111111111","sessionId":"b2222222-2222-4222-8222-222222222222","snapshotId":"c3333333-3333-4333-8333-333333333333","action":"highlight","instruction":"Teste: toque em Agendamento, no botão destacado.","targetElementId":"botao-real"}
                        """) : System.Text.Json.JsonSerializer.SerializeToNode(Contracts.ErrorResponse.Create(code), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
                }
            }
        }
        return Task.CompletedTask;
    }
}
