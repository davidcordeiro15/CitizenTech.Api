using CitizenTech.Api.Contracts;

namespace CitizenTech.Api.Services;

public interface IGuidanceProvider
{
    Task<GuidanceDecision> GenerateAsync(GuidanceRequest request, CancellationToken cancellationToken);
}

public sealed record GuidanceDecision(string Action, string Instruction, string? TargetElementId)
{
    public bool IsValidFor(GuidanceRequest request) => Actions.IsAllowed(Action)
        && !string.IsNullOrWhiteSpace(Instruction) && Instruction.Length <= 250
        && (Action == "highlight"
            ? TargetElementId is not null && request.Screen!.Elements!.Any(e => e!.Id == TargetElementId && e.Enabled)
            : TargetElementId is null);
}

public sealed class FakeGuidanceProvider : IGuidanceProvider
{
    public async Task<GuidanceDecision> GenerateAsync(GuidanceRequest request, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        var element = request.Screen!.Elements!.FirstOrDefault(e => e!.Enabled && e.Text == "Agendamento");
        return element is not null
            ? new("highlight", "Teste: toque em Agendamento, no botão destacado.", element.Id)
            : new("explain", "Esta é uma resposta de teste. Abra a página de demonstração.", null);
    }
}
