namespace MyMemo.Shared.Exceptions;

public class InfographicModerationException : Exception
{
    public string SessionId { get; }
    public string? SanitizedPrompt { get; }

    public InfographicModerationException(
        string sessionId, string message, string? sanitizedPrompt, Exception inner)
        : base(message, inner)
    {
        SessionId = sessionId;
        SanitizedPrompt = sanitizedPrompt;
    }
}
