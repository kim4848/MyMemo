using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using MyMemo.Shared.Models;
using MyMemo.Shared.Repositories;
using MyMemo.Shared.Services;
using MyMemo.Worker.Workers;

namespace MyMemo.Worker.Tests.Workers;

public class MemoGenerationWorkerTests
{
    private readonly ISessionRepository _sessions = Substitute.For<ISessionRepository>();
    private readonly IChunkRepository _chunks = Substitute.For<IChunkRepository>();
    private readonly ITranscriptionRepository _transcriptions = Substitute.For<ITranscriptionRepository>();
    private readonly IMemoRepository _memos = Substitute.For<IMemoRepository>();
    private readonly IMemoGeneratorService _memoGenerator = Substitute.For<IMemoGeneratorService>();
    private readonly MemoGenerationProcessor _sut;

    public MemoGenerationWorkerTests()
    {
        _chunks.GetTranscriptionStatusAsync(Arg.Any<string>()).Returns((2, true));
        _sut = new MemoGenerationProcessor(_sessions, _chunks, _transcriptions, _memos, _memoGenerator, NullLogger<MemoGenerationProcessor>.Instance);
    }

    [Fact]
    public async Task ProcessAsync_GeneratesMemoAndStores()
    {
        _sessions.GetByIdAsync("session-1").Returns(new Session
        {
            Id = "session-1", UserId = "user-1", Status = "processing",
            OutputMode = "full", AudioSource = "microphone",
            StartedAt = "2026-01-01 00:00:00", CreatedAt = "2026-01-01 00:00:00", UpdatedAt = "2026-01-01 00:00:00"
        });

        _transcriptions.ListBySessionAsync("session-1").Returns(new List<Transcription>
        {
            new() { Id = "t1", ChunkId = "c1", RawText = "Hej med dig.", CreatedAt = "2026-01-01 00:00:00" },
            new() { Id = "t2", ChunkId = "c2", RawText = "Hvordan går det?", CreatedAt = "2026-01-01 00:05:00" }
        });

        _memoGenerator.GenerateAsync("Hej med dig.\n\nHvordan går det?", "full", null)
            .Returns(new MemoResult("Renskrevet memo", "gpt-4.1-mini", 100, 50));

        var result = await _sut.ProcessAsync("session-1", dequeueCount: 1);

        result.Should().BeTrue();
        await _memos.Received(1).CreateAsync("session-1", "full", "Renskrevet memo", "gpt-4.1-mini", 100, 50, Arg.Any<long?>());
        await _sessions.Received(1).UpdateStatusAsync("session-1", "completed");
    }

    [Fact]
    public async Task ProcessAsync_RetriesOnError_WhenUnderMaxRetries()
    {
        _sessions.GetByIdAsync("session-1").Returns(new Session
        {
            Id = "session-1", UserId = "user-1", Status = "processing",
            OutputMode = "full", AudioSource = "microphone",
            StartedAt = "2026-01-01 00:00:00", CreatedAt = "2026-01-01 00:00:00", UpdatedAt = "2026-01-01 00:00:00"
        });

        _transcriptions.ListBySessionAsync("session-1").Returns(new List<Transcription>());
        _memoGenerator.GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
            .Throws(new Exception("LLM error"));

        var result = await _sut.ProcessAsync("session-1", dequeueCount: 1);

        result.Should().BeFalse();
        await _sessions.DidNotReceive().UpdateStatusAsync("session-1", "failed");
    }

    [Fact]
    public async Task ProcessAsync_SetsFailedStatus_AfterMaxRetries()
    {
        _sessions.GetByIdAsync("session-1").Returns(new Session
        {
            Id = "session-1", UserId = "user-1", Status = "processing",
            OutputMode = "full", AudioSource = "microphone",
            StartedAt = "2026-01-01 00:00:00", CreatedAt = "2026-01-01 00:00:00", UpdatedAt = "2026-01-01 00:00:00"
        });

        _transcriptions.ListBySessionAsync("session-1").Returns(new List<Transcription>());
        _memoGenerator.GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
            .Throws(new Exception("LLM error"));

        var result = await _sut.ProcessAsync("session-1", dequeueCount: 3);

        result.Should().BeTrue();
        await _sessions.Received(1).UpdateStatusAsync("session-1", "failed");
    }

    [Fact]
    public async Task ProcessAsync_GeneratesMemoWithProductPlanningMode()
    {
        _sessions.GetByIdAsync("session-1").Returns(new Session
        {
            Id = "session-1", UserId = "user-1", Status = "processing",
            OutputMode = "product-planning", AudioSource = "microphone",
            StartedAt = "2026-01-01 00:00:00", CreatedAt = "2026-01-01 00:00:00", UpdatedAt = "2026-01-01 00:00:00"
        });

        _transcriptions.ListBySessionAsync("session-1").Returns(new List<Transcription>
        {
            new() { Id = "t1", ChunkId = "c1", RawText = "Vi skal bygge en ny feature.", CreatedAt = "2026-01-01 00:00:00" }
        });

        _memoGenerator.GenerateAsync("Vi skal bygge en ny feature.", "product-planning", null)
            .Returns(new MemoResult("Produktplan", "gpt-4.1-mini", 80, 40));

        var result = await _sut.ProcessAsync("session-1", dequeueCount: 1);

        result.Should().BeTrue();
        await _memos.Received(1).CreateAsync("session-1", "product-planning", "Produktplan", "gpt-4.1-mini", 80, 40, Arg.Any<long?>());
        await _sessions.Received(1).UpdateStatusAsync("session-1", "completed");
    }

    [Fact]
    public async Task ProcessAsync_PassesContextToGenerator()
    {
        _sessions.GetByIdAsync("session-1").Returns(new Session
        {
            Id = "session-1", UserId = "user-1", Status = "processing",
            OutputMode = "summary", AudioSource = "microphone",
            Context = "Møde med København, deltagere: Anne og Bjarne",
            StartedAt = "2026-01-01 00:00:00", CreatedAt = "2026-01-01 00:00:00", UpdatedAt = "2026-01-01 00:00:00"
        });

        _transcriptions.ListBySessionAsync("session-1").Returns(new List<Transcription>
        {
            new() { Id = "t1", ChunkId = "c1", RawText = "Vi talte om overdragelse.", CreatedAt = "2026-01-01 00:00:00" }
        });

        _memoGenerator.GenerateAsync("Vi talte om overdragelse.", "summary", "Møde med København, deltagere: Anne og Bjarne")
            .Returns(new MemoResult("Referat", "gpt-4.1-mini", 90, 45));

        var result = await _sut.ProcessAsync("session-1", dequeueCount: 1);

        result.Should().BeTrue();
        await _memoGenerator.Received(1).GenerateAsync("Vi talte om overdragelse.", "summary", "Møde med København, deltagere: Anne og Bjarne");
    }

    [Fact]
    public async Task ProcessAsync_SkipsMemoGeneration_WhenZeroChunks()
    {
        _chunks.GetTranscriptionStatusAsync("session-1").Returns((0, true));

        _sessions.GetByIdAsync("session-1").Returns(new Session
        {
            Id = "session-1", UserId = "user-1", Status = "processing",
            OutputMode = "full", AudioSource = "microphone",
            StartedAt = "2026-01-01 00:00:00", CreatedAt = "2026-01-01 00:00:00", UpdatedAt = "2026-01-01 00:00:00"
        });

        var result = await _sut.ProcessAsync("session-1", dequeueCount: 1);

        result.Should().BeTrue();
        await _memoGenerator.DidNotReceive().GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>());
        await _memos.DidNotReceive().CreateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<long?>());
    }

    [Fact]
    public async Task ProcessAsync_ReturnsFalse_WhenChunksNotAllTranscribed()
    {
        _chunks.GetTranscriptionStatusAsync("session-1").Returns((2, false));

        _sessions.GetByIdAsync("session-1").Returns(new Session
        {
            Id = "session-1", UserId = "user-1", Status = "processing",
            OutputMode = "full", AudioSource = "microphone",
            StartedAt = "2026-01-01 00:00:00", CreatedAt = "2026-01-01 00:00:00", UpdatedAt = "2026-01-01 00:00:00"
        });

        var result = await _sut.ProcessAsync("session-1", dequeueCount: 1);

        result.Should().BeFalse();
        await _memoGenerator.DidNotReceive().GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>());
    }
}
