using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MyMemo.Shared.Services;

public sealed class SpeechBatchTranscriptionService : ISpeechBatchTranscriptionService
{
    private readonly HttpClient _httpClient;
    private readonly AzureSpeechOptions _options;
    private readonly ILogger<SpeechBatchTranscriptionService> _logger;

    public SpeechBatchTranscriptionService(
        IOptions<AzureSpeechOptions> options,
        ILogger<SpeechBatchTranscriptionService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _options.ApiKey);
    }

    public async Task<string> SubmitAsync(string sasUrl, string language = "da-DK")
    {
        var baseUrl = _options.Endpoint.TrimEnd('/');
        var requestUrl = $"{baseUrl}/speechtotext/v3.2/transcriptions";

        var requestBody = new
        {
            contentUrls = new[] { sasUrl },
            locale = language,
            displayName = $"MyMemo-{DateTime.UtcNow:yyyyMMdd-HHmmss}",
            properties = new
            {
                diarizationEnabled = true,
                wordLevelTimestampsEnabled = true,
                punctuationMode = "DictatedAndAutomatic",
                profanityFilterMode = "None",
            },
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(requestUrl, content);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<TranscriptionResponse>();
        var jobId = result!.Self.Split('/').Last();

        _logger.LogInformation("Submitted batch transcription job {JobId} for locale {Locale}", jobId, language);
        return jobId;
    }

    public async Task<BatchTranscriptionStatus> GetStatusAsync(string jobId)
    {
        var baseUrl = _options.Endpoint.TrimEnd('/');
        var requestUrl = $"{baseUrl}/speechtotext/v3.2/transcriptions/{jobId}";

        var response = await _httpClient.GetAsync(requestUrl);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<TranscriptionResponse>();
        return result!.Status switch
        {
            "NotStarted" => BatchTranscriptionStatus.NotStarted,
            "Running" => BatchTranscriptionStatus.Running,
            "Succeeded" => BatchTranscriptionStatus.Succeeded,
            "Failed" => BatchTranscriptionStatus.Failed,
            _ => BatchTranscriptionStatus.Running,
        };
    }

    public async Task<BatchTranscriptionResult> GetResultAsync(string jobId)
    {
        var baseUrl = _options.Endpoint.TrimEnd('/');
        var filesUrl = $"{baseUrl}/speechtotext/v3.2/transcriptions/{jobId}/files";

        var filesResponse = await _httpClient.GetAsync(filesUrl);
        filesResponse.EnsureSuccessStatusCode();

        var filesResult = await filesResponse.Content.ReadFromJsonAsync<FilesResponse>();
        var transcriptionFile = filesResult!.Values.FirstOrDefault(f => f.Kind == "Transcription");

        if (transcriptionFile is null)
            throw new InvalidOperationException($"No transcription file found for job {jobId}");

        var contentResponse = await _httpClient.GetAsync(transcriptionFile.Links.ContentUrl);
        contentResponse.EnsureSuccessStatusCode();

        var transcriptionContent = await contentResponse.Content.ReadFromJsonAsync<TranscriptionContent>();
        var segments = new List<SpeakerSegment>();

        if (transcriptionContent?.CombinedRecognizedPhrases != null)
        {
            foreach (var phrase in transcriptionContent.RecognizedPhrases ?? [])
            {
                var best = phrase.NBest?.FirstOrDefault();
                if (best is null) continue;

                var speakerId = phrase.Speaker ?? 0;
                var text = best.Display ?? "";
                var offsetSeconds = TimeSpan.FromTicks(long.Parse(phrase.Offset ?? "0")).TotalSeconds;
                var durationSeconds = TimeSpan.FromTicks(long.Parse(phrase.Duration ?? "0")).TotalSeconds;

                if (!string.IsNullOrWhiteSpace(text))
                {
                    segments.Add(new SpeakerSegment(speakerId, text, offsetSeconds, offsetSeconds + durationSeconds));
                }
            }
        }

        var readableText = BuildReadableText(segments);
        return new BatchTranscriptionResult(segments, readableText);
    }

    public async Task DeleteAsync(string jobId)
    {
        var baseUrl = _options.Endpoint.TrimEnd('/');
        var requestUrl = $"{baseUrl}/speechtotext/v3.2/transcriptions/{jobId}";
        await _httpClient.DeleteAsync(requestUrl);
    }

    private static string BuildReadableText(List<SpeakerSegment> segments)
    {
        if (segments.Count == 0) return "";

        var sb = new StringBuilder();
        var currentSpeaker = -1;

        foreach (var segment in segments)
        {
            if (segment.SpeakerId != currentSpeaker)
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.Append($"Speaker {segment.SpeakerId}: ");
                currentSpeaker = segment.SpeakerId;
            }
            else
            {
                sb.Append(' ');
            }
            sb.Append(segment.Text);
        }

        return sb.ToString();
    }

    // JSON response models
    private sealed class TranscriptionResponse
    {
        [JsonPropertyName("self")]
        public string Self { get; set; } = "";
        [JsonPropertyName("status")]
        public string Status { get; set; } = "";
    }

    private sealed class FilesResponse
    {
        [JsonPropertyName("values")]
        public List<FileEntry> Values { get; set; } = [];
    }

    private sealed class FileEntry
    {
        [JsonPropertyName("kind")]
        public string Kind { get; set; } = "";
        [JsonPropertyName("links")]
        public FileLinks Links { get; set; } = new();
    }

    private sealed class FileLinks
    {
        [JsonPropertyName("contentUrl")]
        public string ContentUrl { get; set; } = "";
    }

    private sealed class TranscriptionContent
    {
        [JsonPropertyName("combinedRecognizedPhrases")]
        public List<object>? CombinedRecognizedPhrases { get; set; }
        [JsonPropertyName("recognizedPhrases")]
        public List<RecognizedPhrase>? RecognizedPhrases { get; set; }
    }

    private sealed class RecognizedPhrase
    {
        [JsonPropertyName("speaker")]
        public int? Speaker { get; set; }
        [JsonPropertyName("offset")]
        public string? Offset { get; set; }
        [JsonPropertyName("duration")]
        public string? Duration { get; set; }
        [JsonPropertyName("nBest")]
        public List<NBestEntry>? NBest { get; set; }
    }

    private sealed class NBestEntry
    {
        [JsonPropertyName("display")]
        public string? Display { get; set; }
    }
}
