namespace MyMemo.Shared.Services;

public sealed class AzureBlobOptions
{
    public required string ConnectionString { get; init; }
    public string ContainerName { get; init; } = "audio-chunks";
}

public sealed class StorageQueueOptions
{
    public required string ConnectionString { get; init; }
    public string TranscriptionQueueName { get; init; } = "transcription-jobs";
    public string MemoGenerationQueueName { get; init; } = "memo-generation";
    public string InfographicGenerationQueueName { get; init; } = "infographic-generation";
}

public sealed class AzureOpenAIOptions
{
    public required string Endpoint { get; init; }
    public required string ApiKey { get; init; }
    public string WhisperDeployment { get; init; } = "whisper-1";
    public string GptDeployment { get; init; } = "gpt-5.3-chat";
    public string ImageDeployment { get; init; } = "gpt-image-1.5";
}

public sealed class AzureSpeechOptions
{
    public required string Endpoint { get; init; }
    public required string SubscriptionKey { get; init; }
}

public sealed class TranscriptionOptions
{
    /// <summary>
    /// Which transcription provider to use: "whisper" (default) or "azure-fast".
    /// </summary>
    public string Provider { get; init; } = "whisper";
}
