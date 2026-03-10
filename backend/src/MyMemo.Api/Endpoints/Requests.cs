namespace MyMemo.Api.Endpoints;

public sealed record CreateSessionRequest(string OutputMode = "full", string AudioSource = "microphone", string? Context = null, string TranscriptionMode = "whisper");
