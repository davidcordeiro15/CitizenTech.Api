using CitizenTech.Api.Contracts;

namespace CitizenTech.Api.Models;

public sealed class SessionEvaluation
{
    private SessionEvaluation() { }
    public SessionEvaluation(Guid sessionId, string outcome, int rating, bool neededHumanHelp, DateTime createdAtUtc)
    {
        if (sessionId == Guid.Empty || !EvaluationOutcomes.IsValid(outcome) || rating is < 1 or > 5 || createdAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Avaliação inválida.");
        Id = Guid.NewGuid();
        SessionId = sessionId;
        Outcome = outcome;
        Rating = rating;
        NeededHumanHelp = neededHumanHelp;
        Source = "manual";
        CreatedAtUtc = createdAtUtc;
    }
    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public string Outcome { get; private set; } = null!;
    public int Rating { get; private set; }
    public bool NeededHumanHelp { get; private set; }
    public string Source { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }
}
