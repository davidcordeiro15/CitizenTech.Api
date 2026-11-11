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
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["AdminKey"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Header, Name = "X-Admin-Key",
            Description = "Chave administrativa configurada exclusivamente no ambiente do servidor. Não compartilhar em prints ou commits."
        };
        foreach (var (route, adminPath) in document.Paths)
        {
            if (!route.StartsWith("/api/v1/admin/", StringComparison.Ordinal) && route != "/api/v1/sessions/{sessionId}/evaluation") continue;
            if (adminPath.Operations is null) continue;
            foreach (var operation in adminPath.Operations.Values)
            {
                operation.Security = [new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("AdminKey", document)] = []
                }];
                operation.Description = "Requer X-Admin-Key. Erros usam envelope v1 com requestId de correlação gerado pelo servidor. " + (route.EndsWith("/evaluation")
                    ? "Coleta manual, única por sessão existente; outcome: completed, partially_completed ou not_completed; rating inteiro 1..5 e neededHumanHelp boolean obrigatório. Retorna 201 somente após gravação, sem Location."
                    : route.EndsWith("/metrics")
                    ? "Bases distintas: GUIDANCE_RECORDS criados no intervalo e SESSION_EVALUATIONS criadas no intervalo, sem conversões entre populações. Percentuais 0..100, duas casas; denominador zero e média vazia retornam null. Sucesso técnico não comprova conclusão; falhas não persistidas não são contabilizadas."
                    : "Resumo completo da atividade observada (não duração da tarefa), sem conteúdo pessoal. Paginação page=1, pageSize=20, máximo 100. Listagem seleciona sessões com algum registro no intervalo e filtra o resumo completo; hasEvaluation=false com outcome é inválido.");
                if (!route.Contains("{sessionId}")) operation.Description += " from/to ISO 8601 com timezone explícito: from inclusivo, to exclusivo; informar ambos ou nenhum (últimos 30 dias UTC). Retorna intervalo efetivo.";
                if (operation.RequestBody?.Content?.TryGetValue("application/json", out var input) == true)
                    input.Example = JsonNode.Parse("""{"outcome":"completed","rating":4,"neededHumanHelp":true}""");
                foreach (var (status, response) in operation.Responses ?? [])
                {
                    var description = status switch
                    {
                        "400" => "INVALID_REQUEST", "401" => "UNAUTHORIZED",
                        "404" => "SESSION_NOT_FOUND", "409" => "SESSION_ALREADY_EVALUATED",
                        "429" => "RATE_LIMITED", "503" => "ADMIN_NOT_CONFIGURED ou PERSISTENCE_UNAVAILABLE", _ => null
                    };
                    if (description is not null) response.Description = description;
                }
            }
        }
        return Task.CompletedTask;
    }
}
