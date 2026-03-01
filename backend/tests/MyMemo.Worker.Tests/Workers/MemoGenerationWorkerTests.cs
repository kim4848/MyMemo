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
        _chunks.AreAllTranscribedAsync(Arg.Any<string>()).Returns(true);
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

        _memoGenerator.GenerateAsync("Hej med dig.\n\nHvordan går det?", "full")
            .Returns(new MemoResult("Renskrevet memo", "gpt-4.1-nano", 100, 50));

        await _sut.ProcessAsync("session-1");

        await _memos.Received(1).CreateAsync("session-1", "full", "Renskrevet memo", "gpt-4.1-nano", 100, 50);
        await _sessions.Received(1).UpdateStatusAsync("session-1", "completed");
    }

    [Fact]
    public async Task ProcessAsync_SetsFailedStatus_OnError()
    {
        _sessions.GetByIdAsync("session-1").Returns(new Session
        {
            Id = "session-1", UserId = "user-1", Status = "processing",
            OutputMode = "full", AudioSource = "microphone",
            StartedAt = "2026-01-01 00:00:00", CreatedAt = "2026-01-01 00:00:00", UpdatedAt = "2026-01-01 00:00:00"
        });

        _transcriptions.ListBySessionAsync("session-1").Returns(new List<Transcription>());
        _memoGenerator.GenerateAsync(Arg.Any<string>(), Arg.Any<string>())
            .Throws(new Exception("LLM error"));

        await _sut.ProcessAsync("session-1");

        await _sessions.Received(1).UpdateStatusAsync("session-1", "failed");
    }
}
