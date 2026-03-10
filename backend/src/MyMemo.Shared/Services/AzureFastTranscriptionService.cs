using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MyMemo.Shared.Services;

public class AzureFastTranscriptionService : IWhisperService
{
    private readonly HttpClient _httpClient;
    private readonly AzureSpeechOptions _options;
    private readonly ILogger<AzureFastTranscriptionService> _logger;

    public AzureFastTranscriptionService(
        HttpClient httpClient,
        IOptions<AzureSpeechOptions> options,
        ILogger<AzureFastTranscriptionService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<WhisperResult> TranscribeAsync(Stream audioStream, string language = "da")
    {
        var endpoint = _options.Endpoint.TrimEnd('/');
        var url = $"{endpoint}/speechtotext/transcriptions:transcribe?api-version=2024-11-15";

        using var content = new MultipartFormDataContent();

        // Audio file part
        var audioContent = new StreamContent(audioStream);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/webm");
        content.Add(audioContent, "audio", "audio.webm");

        // Definition part — configure diarization + Danish locale
        var definition = new FastTranscriptionDefinition
        {
            Locales = [MapLanguageToLocale(language)],
            Diarization = new DiarizationDefinition { Enabled = true },
        };
        var definitionJson = JsonSerializer.Serialize(definition, JsonContext.Default.FastTranscriptionDefinition);
        var definitionContent = new StringContent(definitionJson);
        definitionContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        content.Add(definitionContent, "definition");

        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        request.Headers.Add("Ocp-Apim-Subscription-Key", _options.SubscriptionKey);

        _logger.LogInformation("Sending fast transcription request for locale {Locale} with diarization enabled",
            MapLanguageToLocale(language));

        using var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Fast transcription failed with status {StatusCode}: {Body}",
                response.StatusCode, responseBody);
            throw new HttpRequestException(
                $"Azure Fast Transcription API returned {(int)response.StatusCode}: {responseBody}");
        }

        var result = JsonSerializer.Deserialize(responseBody, JsonContext.Default.FastTranscriptionResponse)
            ?? throw new InvalidOperationException("Failed to deserialize transcription response");

        var combinedText = result.CombinedPhrases?.Count > 0
            ? string.Join("\n", result.CombinedPhrases.Select(p => p.Text))
            : string.Join(" ", result.Phrases?.Select(p => p.Text) ?? []);

        double? avgConfidence = result.Phrases?.Count > 0
            ? result.Phrases.Average(p => p.Confidence)
            : null;

        string? wordTimestamps = BuildWordTimestamps(result.Phrases);

        return new WhisperResult(combinedText, avgConfidence, wordTimestamps);
    }

    private static string? BuildWordTimestamps(List<FastTranscriptionPhrase>? phrases)
    {
        if (phrases is null || phrases.Count == 0)
            return null;

        var words = new List<object>();
        foreach (var phrase in phrases)
        {
            if (phrase.Words is null) continue;
            foreach (var w in phrase.Words)
            {
                words.Add(new
                {
                    word = w.Text,
                    start = ParseDurationToSeconds(w.OffsetMilliseconds),
                    end = ParseDurationToSeconds(w.OffsetMilliseconds + w.DurationMilliseconds),
                    speaker = phrase.Speaker
                });
            }
        }

        return words.Count > 0 ? JsonSerializer.Serialize(words) : null;
    }

    private static double ParseDurationToSeconds(long milliseconds) =>
        milliseconds / 1000.0;

    internal static string MapLanguageToLocale(string language) =>
        language.Contains('-') ? language : language switch
        {
            "da" => "da-DK",
            "en" => "en-US",
            "de" => "de-DE",
            "sv" => "sv-SE",
            "nb" => "nb-NO",
            "nn" => "nn-NO",
            _ => $"{language}-{language.ToUpperInvariant()}"
        };
}

// Request models

internal sealed class FastTranscriptionDefinition
{
    [JsonPropertyName("locales")]
    public required List<string> Locales { get; init; }

    [JsonPropertyName("diarization")]
    public DiarizationDefinition? Diarization { get; init; }
}

internal sealed class DiarizationDefinition
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }
}

// Response models

internal sealed class FastTranscriptionResponse
{
    [JsonPropertyName("combinedPhrases")]
    public List<CombinedPhrase>? CombinedPhrases { get; init; }

    [JsonPropertyName("phrases")]
    public List<FastTranscriptionPhrase>? Phrases { get; init; }
}

internal sealed class CombinedPhrase
{
    [JsonPropertyName("text")]
    public string Text { get; init; } = "";
}

internal sealed class FastTranscriptionPhrase
{
    [JsonPropertyName("text")]
    public string Text { get; init; } = "";

    [JsonPropertyName("confidence")]
    public double Confidence { get; init; }

    [JsonPropertyName("speaker")]
    public int Speaker { get; init; }

    [JsonPropertyName("offsetMilliseconds")]
    public long OffsetMilliseconds { get; init; }

    [JsonPropertyName("durationMilliseconds")]
    public long DurationMilliseconds { get; init; }

    [JsonPropertyName("words")]
    public List<FastTranscriptionWord>? Words { get; init; }
}

internal sealed class FastTranscriptionWord
{
    [JsonPropertyName("text")]
    public string Text { get; init; } = "";

    [JsonPropertyName("offsetMilliseconds")]
    public long OffsetMilliseconds { get; init; }

    [JsonPropertyName("durationMilliseconds")]
    public long DurationMilliseconds { get; init; }
}

[JsonSerializable(typeof(FastTranscriptionDefinition))]
[JsonSerializable(typeof(FastTranscriptionResponse))]
internal sealed partial class JsonContext : JsonSerializerContext;
