using System.ClientModel;
using System.Text.Json;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using OpenAI.Audio;

namespace MyMemo.Shared.Services;

public class WhisperService : IWhisperService
{
    private readonly Lazy<AudioClient> _audioClient;

    public WhisperService(IOptions<AzureOpenAIOptions> options)
    {
        _audioClient = new Lazy<AudioClient>(() =>
        {
            var credential = new ApiKeyCredential(options.Value.ApiKey);
            var client = new AzureOpenAIClient(new Uri(options.Value.Endpoint), credential);
            return client.GetAudioClient(options.Value.WhisperDeployment);
        });
    }

    protected WhisperService(AudioClient audioClient)
    {
        _audioClient = new Lazy<AudioClient>(audioClient);
    }

    public async Task<WhisperResult> TranscribeAsync(Stream audioStream, string language = "da")
    {
        // Buffer into MemoryStream so the SDK's retry policy can re-read the stream
        var memoryStream = new MemoryStream();
        await audioStream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        return await TranscribeBufferedAsync(memoryStream, language);
    }

    protected virtual async Task<WhisperResult> TranscribeBufferedAsync(MemoryStream audioStream, string language)
    {
        var audioClient = _audioClient.Value;

        var transcriptionOptions = new AudioTranscriptionOptions
        {
            Language = language,
            ResponseFormat = AudioTranscriptionFormat.Verbose
        };

        var result = await audioClient.TranscribeAudioAsync(audioStream, "audio.webm", transcriptionOptions);
        var transcription = result.Value;

        string? wordTimestamps = null;
        if (transcription.Words?.Count > 0)
        {
            var words = transcription.Words.Select(w => new { word = w.Word, start = w.StartTime.TotalSeconds, end = w.EndTime.TotalSeconds });
            wordTimestamps = JsonSerializer.Serialize(words);
        }

        return new WhisperResult(transcription.Text, null, wordTimestamps);
    }
}
