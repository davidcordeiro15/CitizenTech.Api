namespace CitizenTech.Api.Models;

/// <summary>Technical metadata only; never stores page content or generated instructions.</summary>
public sealed class GuidanceRecord
{
    private GuidanceRecord() { }

    public GuidanceRecord(Guid requestId, Guid sessionId, DateTime createdAtUtc,
        string? action, string result, string? errorCode)
    {
        if (requestId == Guid.Empty) throw new ArgumentException("A request ID is required.", nameof(requestId));
        if (sessionId == Guid.Empty) throw new ArgumentException("A session ID is required.", nameof(sessionId));
        if (createdAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC is required.", nameof(createdAtUtc));
        if (action is not null and not ("highlight" or "explain" or "ask" or "complete"))
            throw new ArgumentException("Unknown action.", nameof(action));
        if (result is not ("success" or "failure")) throw new ArgumentException("Unknown result.", nameof(result));
        if (result == "success" && (action is null || errorCode is not null))
            throw new ArgumentException("Success requires an action and no error code.");
        if (result == "failure" && string.IsNullOrWhiteSpace(errorCode))
            throw new ArgumentException("Failure requires an error code.", nameof(errorCode));
        if (errorCode is { Length: > 64 }) throw new ArgumentException("Error code exceeds 64 characters.", nameof(errorCode));

        RequestId = requestId;
        SessionId = sessionId;
        CreatedAtUtc = createdAtUtc;
        Action = action;
        Result = result;
        ErrorCode = errorCode;
    }

    public static GuidanceRecord Success(Guid requestId, Guid sessionId, string action) =>
        new(requestId, sessionId, DateTime.UtcNow, action, "success", null);

    public Guid RequestId { get; private set; }
    public Guid SessionId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public string? Action { get; private set; }
    public string Result { get; private set; } = null!;
    public string? ErrorCode { get; private set; }
}
