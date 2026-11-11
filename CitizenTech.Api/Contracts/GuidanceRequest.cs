using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CitizenTech.Api.Contracts;

public sealed class GuidanceRequest : IValidatableObject
{
    [JsonRequired] public string? ContractVersion { get; init; }
    [JsonRequired] public Guid RequestId { get; init; }
    [JsonRequired] public Guid SessionId { get; init; }
    [JsonRequired] public string? Locale { get; init; }
    [JsonRequired] public string? Goal { get; init; }
    [JsonRequired] public string? UserMessage { get; init; }
    [JsonRequired] public List<HistoryItem?>? History { get; init; }
    [JsonRequired] public ScreenSnapshot? Screen { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!IsValid()) yield return new ValidationResult("Requisição fora do contrato v1.");
    }

    // Também utilizado pelo serviço, de modo que chamadas fora do MVC mantenham as invariantes.
    public bool IsValid() => ContractVersion == "1" && RequestId != Guid.Empty && SessionId != Guid.Empty
        && Locale == "pt-BR" && !string.IsNullOrWhiteSpace(Goal) && Goal.Length <= 500
        && (UserMessage is null || UserMessage.Length <= 500)
        && History is { Count: <= 3 } && History.All(h => h is not null && Actions.IsAllowed(h.Action)
            && h.Instruction is not null && h.Instruction.Length <= 250)
        && Screen is { SnapshotId: var snapshotId, PackageName: var package, Elements: { Count: >= 1 and <= 80 } elements }
        && snapshotId != Guid.Empty && !string.IsNullOrWhiteSpace(package)
        && elements.All(e => e is not null && !string.IsNullOrWhiteSpace(e.Id) && e.Id.Length <= 40
            && Roles.Contains(e.Role ?? "") && e.Text is { Length: <= 160 })
        && elements.Select(e => e!.Id).Distinct(StringComparer.Ordinal).Count() == elements.Count;

    private static readonly HashSet<string> Roles = ["button", "link", "input", "text", "scroll", "other"];
}

public sealed class HistoryItem
{
    [JsonRequired] public string? Action { get; init; }
    [JsonRequired] public string? Instruction { get; init; }
}

public sealed class ScreenSnapshot
{
    [JsonRequired] public Guid SnapshotId { get; init; }
    [JsonRequired] public string? PackageName { get; init; }
    [JsonRequired] public List<ScreenElement?>? Elements { get; init; }
}

public sealed class ScreenElement
{
    [JsonRequired] public string? Id { get; init; }
    [JsonRequired] public string? Role { get; init; }
    [JsonRequired] public string? Text { get; init; }
    [JsonRequired] public bool Enabled { get; init; }
}

public static class Actions
{
    public static bool IsAllowed(string? action) => action is "highlight" or "explain" or "ask" or "complete";
}
