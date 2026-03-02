namespace MyMemo.Api.Endpoints;

public sealed record CreateSessionRequest(string OutputMode = "full", string AudioSource = "microphone");
