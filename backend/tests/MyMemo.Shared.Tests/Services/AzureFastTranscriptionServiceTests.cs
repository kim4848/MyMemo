using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MyMemo.Shared.Services;

namespace MyMemo.Shared.Tests.Services;

public class AzureFastTranscriptionServiceTests
{
    [Theory]
    [InlineData("da", "da-DK")]
    [InlineData("en", "en-US")]
    [InlineData("de", "de-DE")]
    [InlineData("sv", "sv-SE")]
    [InlineData("nb", "nb-NO")]
    [InlineData("da-DK", "da-DK")]
    [InlineData("en-US", "en-US")]
    public void MapLanguageToLocale_ReturnsCorrectLocale(string input, string expected)
    {
        AzureFastTranscriptionService.MapLanguageToLocale(input).Should().Be(expected);
    }

    [Fact]
    public async Task TranscribeAsync_SendsCorrectRequestAndParsesResponse()
    {
        var responseBody = JsonSerializer.Serialize(new
        {
            combinedPhrases = new[]
            {
                new { text = "Hej med dig. Hvordan har du det?" }
            },
            phrases = new[]
            {
                new
                {
                    text = "Hej med dig.",
                    confidence = 0.95,
                    speaker = 1,
                    offsetMilliseconds = 0L,
                    durationMilliseconds = 2000L,
                    words = new[]
                    {
                        new { text = "Hej", offsetMilliseconds = 0L, durationMilliseconds = 500L },
                        new { text = "med", offsetMilliseconds = 500L, durationMilliseconds = 500L },
                        new { text = "dig.", offsetMilliseconds = 1000L, durationMilliseconds = 500L }
                    }
                },
                new
                {
                    text = "Hvordan har du det?",
                    confidence = 0.92,
                    speaker = 2,
                    offsetMilliseconds = 2500L,
                    durationMilliseconds = 3000L,
                    words = new[]
                    {
                        new { text = "Hvordan", offsetMilliseconds = 2500L, durationMilliseconds = 700L },
                        new { text = "har", offsetMilliseconds = 3200L, durationMilliseconds = 400L },
                        new { text = "du", offsetMilliseconds = 3600L, durationMilliseconds = 400L },
                        new { text = "det?", offsetMilliseconds = 4000L, durationMilliseconds = 500L }
                    }
                }
            }
        });

        var handler = new FakeHttpHandler(HttpStatusCode.OK, responseBody);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://fake.cognitiveservices.azure.com") };
        var options = Options.Create(new AzureSpeechOptions
        {
            Endpoint = "https://fake.cognitiveservices.azure.com",
            SubscriptionKey = "fake-key"
        });

        var service = new AzureFastTranscriptionService(httpClient, options, NullLogger<AzureFastTranscriptionService>.Instance);
        var audioStream = new MemoryStream(new byte[] { 1, 2, 3 });

        var result = await service.TranscribeAsync(audioStream, "da");

        result.Text.Should().Be("Hej med dig. Hvordan har du det?");
        result.AverageConfidence.Should().BeApproximately(0.935, 0.001);
        result.WordTimestampsJson.Should().NotBeNull();

        // Verify the request was sent correctly
        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.RequestUri!.ToString().Should().Contain("speechtotext/transcriptions:transcribe");
        handler.LastRequest.Headers.GetValues("Ocp-Apim-Subscription-Key").Should().ContainSingle("fake-key");
    }

    [Fact]
    public async Task TranscribeAsync_WordTimestamps_IncludeSpeakerInfo()
    {
        var responseBody = JsonSerializer.Serialize(new
        {
            combinedPhrases = new[] { new { text = "Hej" } },
            phrases = new[]
            {
                new
                {
                    text = "Hej",
                    confidence = 0.95,
                    speaker = 1,
                    offsetMilliseconds = 0L,
                    durationMilliseconds = 500L,
                    words = new[]
                    {
                        new { text = "Hej", offsetMilliseconds = 0L, durationMilliseconds = 500L }
                    }
                }
            }
        });

        var handler = new FakeHttpHandler(HttpStatusCode.OK, responseBody);
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new AzureSpeechOptions
        {
            Endpoint = "https://fake.cognitiveservices.azure.com",
            SubscriptionKey = "fake-key"
        });

        var service = new AzureFastTranscriptionService(httpClient, options, NullLogger<AzureFastTranscriptionService>.Instance);
        var result = await service.TranscribeAsync(new MemoryStream([1, 2, 3]), "da");

        result.WordTimestampsJson.Should().Contain("\"speaker\":1");
    }

    [Fact]
    public async Task TranscribeAsync_ThrowsOnNonSuccessStatusCode()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.BadRequest, "{\"error\":\"Invalid audio\"}");
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new AzureSpeechOptions
        {
            Endpoint = "https://fake.cognitiveservices.azure.com",
            SubscriptionKey = "fake-key"
        });

        var service = new AzureFastTranscriptionService(httpClient, options, NullLogger<AzureFastTranscriptionService>.Instance);

        var act = () => service.TranscribeAsync(new MemoryStream([1, 2, 3]), "da");
        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*400*");
    }

    [Fact]
    public async Task TranscribeAsync_FallsBackToPhraseText_WhenNoCombinedPhrases()
    {
        var responseBody = JsonSerializer.Serialize(new
        {
            phrases = new[]
            {
                new { text = "Hej", confidence = 0.9, speaker = 1, offsetMilliseconds = 0L, durationMilliseconds = 500L, words = (object[]?)null },
                new { text = "verden", confidence = 0.8, speaker = 2, offsetMilliseconds = 600L, durationMilliseconds = 500L, words = (object[]?)null }
            }
        });

        var handler = new FakeHttpHandler(HttpStatusCode.OK, responseBody);
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new AzureSpeechOptions
        {
            Endpoint = "https://fake.cognitiveservices.azure.com",
            SubscriptionKey = "fake-key"
        });

        var service = new AzureFastTranscriptionService(httpClient, options, NullLogger<AzureFastTranscriptionService>.Instance);
        var result = await service.TranscribeAsync(new MemoryStream([1, 2, 3]), "da");

        result.Text.Should().Be("Hej verden");
        result.AverageConfidence.Should().BeApproximately(0.85, 0.001);
    }

    private sealed class FakeHttpHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
