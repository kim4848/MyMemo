using System.ClientModel;
using System.Text.Json;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using OpenAI.Audio;

namespace MyMemo.Shared.Services;

public sealed class WhisperService(IOptions<AzureOpenAIOptions> options) : IWhisperService
{
    public async Task<WhisperResult> TranscribeAsync(Stream audioStream, string language = "da")
    {
        var credential = new ApiKeyCredential(options.Value.ApiKey);
        var client = new AzureOpenAIClient(new Uri(options.Value.Endpoint), credential);
        var audioClient = client.GetAudioClient(options.Value.WhisperDeployment);

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
