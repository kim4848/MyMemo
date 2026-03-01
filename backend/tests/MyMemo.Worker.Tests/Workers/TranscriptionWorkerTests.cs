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
    private readonly IMemoTriggerService _memoTrigger = Substitute.For<IMemoTriggerService>();
    private readonly TranscriptionProcessor _sut;

    public TranscriptionWorkerTests()
    {
        _sut = new TranscriptionProcessor(_chunks, _transcriptions, _blobService, _whisperService, _memoTrigger, NullLogger<TranscriptionProcessor>.Instance);
    }

    [Fact]
    public async Task ProcessAsync_TranscribesChunkAndStoresResult()
    {
        var audioStream = new MemoryStream(new byte[] { 1, 2, 3 });
        _blobService.DownloadAsync("path/0.webm").Returns(audioStream);
        _whisperService.TranscribeAsync(audioStream, "da")
            .Returns(new WhisperResult("Hej med dig", 0.95, null));

        await _sut.ProcessAsync("session-1", "chunk-1", 0, "path/0.webm", "da");

        await _chunks.Received(1).UpdateStatusAsync("chunk-1", "transcribing");
        await _transcriptions.Received(1).CreateAsync("chunk-1", "Hej med dig", "da", 0.95, null, Arg.Any<long?>());
        await _chunks.Received(1).UpdateStatusAsync("chunk-1", "transcribed");
    }

    [Fact]
    public async Task ProcessAsync_CallsTryQueueMemoGeneration_AfterTranscription()
    {
        var audioStream = new MemoryStream(new byte[] { 1, 2, 3 });
        _blobService.DownloadAsync("path/0.webm").Returns(audioStream);
        _whisperService.TranscribeAsync(audioStream, "da")
            .Returns(new WhisperResult("Hej", null, null));

        await _sut.ProcessAsync("session-1", "chunk-1", 0, "path/0.webm", "da");

        await _memoTrigger.Received(1).TryQueueMemoGenerationAsync("session-1");
    }

    [Fact]
    public async Task ProcessAsync_DoesNotCallMemoTrigger_OnFailure()
    {
        _blobService.DownloadAsync("path/0.webm").Throws(new Exception("Blob not found"));

        await _sut.ProcessAsync("session-1", "chunk-1", 0, "path/0.webm", "da");

        await _chunks.Received(1).UpdateStatusAsync("chunk-1", "failed", "Blob not found");
        await _memoTrigger.DidNotReceive().TryQueueMemoGenerationAsync(Arg.Any<string>());
    }
}
