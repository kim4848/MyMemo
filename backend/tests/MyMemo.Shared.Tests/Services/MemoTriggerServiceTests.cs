using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using MyMemo.Shared.Repositories;
using MyMemo.Shared.Services;

namespace MyMemo.Shared.Tests.Services;

public class MemoTriggerServiceTests
{
    private readonly ISessionRepository _sessions = Substitute.For<ISessionRepository>();
    private readonly IChunkRepository _chunks = Substitute.For<IChunkRepository>();
    private readonly IQueueService _queueService = Substitute.For<IQueueService>();
    private readonly MemoTriggerService _sut;

    public MemoTriggerServiceTests()
    {
        _sut = new MemoTriggerService(_sessions, _chunks, _queueService, NullLogger<MemoTriggerService>.Instance);
    }

    [Fact]
    public async Task DoesNotQueue_WhenSessionNotFinalized()
    {
        _sessions.IsFinalizedAsync("session-1").Returns(false);

        var result = await _sut.TryQueueMemoGenerationAsync("session-1");

        result.Should().BeFalse();
        await _queueService.DidNotReceive().SendMemoGenerationJobAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task DoesNotQueue_WhenZeroChunks()
    {
        _sessions.IsFinalizedAsync("session-1").Returns(true);
        _chunks.GetTranscriptionStatusAsync("session-1").Returns((0, true));

        var result = await _sut.TryQueueMemoGenerationAsync("session-1");

        result.Should().BeFalse();
        await _queueService.DidNotReceive().SendMemoGenerationJobAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task DoesNotQueue_WhenNotAllTranscribed()
    {
        _sessions.IsFinalizedAsync("session-1").Returns(true);
        _chunks.GetTranscriptionStatusAsync("session-1").Returns((2, false));

        var result = await _sut.TryQueueMemoGenerationAsync("session-1");

        result.Should().BeFalse();
        await _queueService.DidNotReceive().SendMemoGenerationJobAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task QueuesJob_WhenFinalizedAndAllTranscribed()
    {
        _sessions.IsFinalizedAsync("session-1").Returns(true);
        _chunks.GetTranscriptionStatusAsync("session-1").Returns((2, true));
        _sessions.TrySetMemoQueuedAsync("session-1").Returns(true);

        var result = await _sut.TryQueueMemoGenerationAsync("session-1");

        result.Should().BeTrue();
        await _queueService.Received(1).SendMemoGenerationJobAsync("session-1");
    }

    [Fact]
    public async Task DoesNotQueue_WhenAlreadyQueued()
    {
        _sessions.IsFinalizedAsync("session-1").Returns(true);
        _chunks.GetTranscriptionStatusAsync("session-1").Returns((2, true));
        _sessions.TrySetMemoQueuedAsync("session-1").Returns(false);

        var result = await _sut.TryQueueMemoGenerationAsync("session-1");

        result.Should().BeFalse();
        await _queueService.DidNotReceive().SendMemoGenerationJobAsync(Arg.Any<string>());
    }
}
