using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using MyMemo.Shared.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using MyMemo.Shared.Services;
using MyMemo.Worker.Workers;

namespace MyMemo.Worker.Tests.Workers;

public class TranscriptionWorkerTests
{
    private readonly IChunkRepository _chunks = Substitute.For<IChunkRepository>();
    private readonly ITranscriptionRepository _transcriptions = Substitute.For<ITranscriptionRepository>();
    private readonly IBlobStorageService _blobService = Substitute.For<IBlobStorageService>();
    private readonly IWhisperService _whisperService = Substitute.For<IWhisperService>();
    private readonly IQueueService _queueService = Substitute.For<IQueueService>();
    private readonly TranscriptionProcessor _sut;

    public TranscriptionWorkerTests()
    {
        _sut = new TranscriptionProcessor(_chunks, _transcriptions, _blobService, _whisperService, _queueService, NullLogger<TranscriptionProcessor>.Instance);
    }

    [Fact]
    public async Task ProcessAsync_TranscribesChunkAndStoresResult()
    {
        var audioStream = new MemoryStream(new byte[] { 1, 2, 3 });
        _blobService.DownloadAsync("path/0.webm").Returns(audioStream);
        _whisperService.TranscribeAsync(audioStream, "da")
            .Returns(new WhisperResult("Hej med dig", 0.95, null));
        _chunks.AreAllTranscribedAsync("session-1").Returns(false);

        await _sut.ProcessAsync("session-1", "chunk-1", 0, "path/0.webm", "da");

        await _chunks.Received(1).UpdateStatusAsync("chunk-1", "transcribing");
        await _transcriptions.Received(1).CreateAsync("chunk-1", "Hej med dig", "da", 0.95, null);
        await _chunks.Received(1).UpdateStatusAsync("chunk-1", "transcribed");
    }

    [Fact]
    public async Task ProcessAsync_EnqueuesMemoGeneration_WhenAllChunksTranscribed()
    {
        var audioStream = new MemoryStream(new byte[] { 1, 2, 3 });
        _blobService.DownloadAsync("path/0.webm").Returns(audioStream);
        _whisperService.TranscribeAsync(audioStream, "da")
            .Returns(new WhisperResult("Hej", null, null));
        _chunks.AreAllTranscribedAsync("session-1").Returns(true);

        await _sut.ProcessAsync("session-1", "chunk-1", 0, "path/0.webm", "da");

        await _queueService.Received(1).SendMemoGenerationJobAsync("session-1");
    }

    [Fact]
    public async Task ProcessAsync_SetsFailedStatus_OnError()
    {
        _blobService.DownloadAsync("path/0.webm").Throws(new Exception("Blob not found"));

        await _sut.ProcessAsync("session-1", "chunk-1", 0, "path/0.webm", "da");

        await _chunks.Received(1).UpdateStatusAsync("chunk-1", "failed", "Blob not found");
    }
}
