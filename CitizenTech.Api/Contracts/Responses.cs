namespace CitizenTech.Api.Contracts;

public sealed record HealthResponse(string Status = "ok", string ContractVersion = "1");
public sealed record GuidanceResponse(string ContractVersion, Guid RequestId, Guid SessionId,
    Guid SnapshotId, string Action, string Instruction, string? TargetElementId);
public sealed record ErrorDetail(string Code, string Message);
public sealed record ErrorResponse(string ContractVersion, Guid? RequestId, ErrorDetail Error)
{
    public static ErrorResponse Create(string code, Guid? requestId = null) => new("1", requestId,
        new(code, code switch
        {
            "UNAUTHORIZED" => "Acesso administrativo não autorizado.",
            "ADMIN_NOT_CONFIGURED" => "Acesso administrativo indisponível.",
            "SESSION_NOT_FOUND" => "Sessão não encontrada.",
            "SESSION_ALREADY_EVALUATED" => "Esta sessão já foi avaliada.",
            "INVALID_REQUEST" => "Requisição inválida. Confira os campos enviados.",
            "REQUEST_ALREADY_RECORDED" => "Esta solicitação já foi registrada. Peça uma nova orientação.",
            "REQUEST_TOO_LARGE" => "A requisição excede o limite de 64 KiB.",
            "RATE_LIMITED" => "Muitas solicitações. Aguarde e tente novamente.",
            "PERSISTENCE_UNAVAILABLE" => "Não consegui registrar esta orientação. Tente novamente.",
            _ => "Não consegui orientar agora. Tente novamente."
        }));
}
