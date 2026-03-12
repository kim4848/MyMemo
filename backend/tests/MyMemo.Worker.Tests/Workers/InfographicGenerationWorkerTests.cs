using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using MyMemo.Shared.Exceptions;
using MyMemo.Shared.Models;
using MyMemo.Shared.Repositories;
using MyMemo.Shared.Services;
using MyMemo.Worker.Workers;

namespace MyMemo.Worker.Tests.Workers;

public class InfographicGenerationWorkerTests
{
    private readonly IMemoRepository _memos = Substitute.For<IMemoRepository>();
    private readonly IInfographicRepository _infographics = Substitute.For<IInfographicRepository>();
    private readonly IInfographicService _infographicService = Substitute.For<IInfographicService>();
    private readonly InfographicGenerationProcessor _sut;

    public InfographicGenerationWorkerTests()
    {
        _sut = new InfographicGenerationProcessor(
            _memos,
            _infographics,
            _infographicService,
            NullLogger<InfographicGenerationProcessor>.Instance);
    }

    [Fact]
    public async Task ProcessAsync_GeneratesInfographic_WhenMemoExists()
    {
        _infographics.GetBySessionIdAsync("session-1").Returns((Infographic?)null);
        _memos.GetBySessionIdAsync("session-1").Returns(new Memo
        {
            Id = "m1", SessionId = "session-1", OutputMode = "full",
            Content = "Memo content", ModelUsed = "gpt-5.3-chat",
            PromptTokens = 100, CompletionTokens = 50, CreatedAt = "2026-01-01 00:00:00"
        });
        _infographicService.GenerateAsync("session-1", "Memo content", "full")
            .Returns(new InfographicResult("base64img", "gpt-image-1.5", null, null));

        var result = await _sut.ProcessAsync("session-1");

        result.Should().BeTrue();
        await _infographics.Received(1).CreateAsync("session-1", "base64img", "gpt-image-1.5", null, null, Arg.Any<long>());
    }

    [Fact]
    public async Task ProcessAsync_SkipsIfInfographicAlreadyExists()
    {
        _infographics.GetBySessionIdAsync("session-1").Returns(new Infographic
        {
            Id = "i1", SessionId = "session-1", ImageContent = "existing",
            ModelUsed = "gpt-image-1.5", CreatedAt = "2026-01-01 00:00:00"
        });

        var result = await _sut.ProcessAsync("session-1");

        result.Should().BeTrue();
        await _infographicService.DidNotReceive().GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ProcessAsync_ModerationBlock_ReturnsTrue_NoRetry()
    {
        _infographics.GetBySessionIdAsync("session-1").Returns((Infographic?)null);
        _memos.GetBySessionIdAsync("session-1").Returns(new Memo
        {
            Id = "m1", SessionId = "session-1", OutputMode = "full",
            Content = "Sensitive content", ModelUsed = "gpt-5.3-chat",
            PromptTokens = 100, CompletionTokens = 50, CreatedAt = "2026-01-01 00:00:00"
        });
        _infographicService.GenerateAsync("session-1", "Sensitive content", "full")
            .Throws(new InfographicModerationException(
                "session-1", "Content blocked", "sanitized prompt", new Exception("inner")));

        var result = await _sut.ProcessAsync("session-1");

        result.Should().BeTrue(); // Handled permanently — no retry
    }

    [Fact]
    public async Task ProcessAsync_TransientError_ReturnsFalse()
    {
        _infographics.GetBySessionIdAsync("session-1").Returns((Infographic?)null);
        _memos.GetBySessionIdAsync("session-1").Returns(new Memo
        {
            Id = "m1", SessionId = "session-1", OutputMode = "full",
            Content = "Content", ModelUsed = "gpt-5.3-chat",
            PromptTokens = 100, CompletionTokens = 50, CreatedAt = "2026-01-01 00:00:00"
        });
        _infographicService.GenerateAsync("session-1", "Content", "full")
            .Throws(new HttpRequestException("Transient network error"));

        // After the error, no infographic exists (not a race condition)
        _infographics.GetBySessionIdAsync("session-1").Returns((Infographic?)null);

        var result = await _sut.ProcessAsync("session-1");

        result.Should().BeFalse(); // Should be retried
    }

    [Fact]
    public async Task ProcessAsync_SkipsWhenNoMemo()
    {
        _infographics.GetBySessionIdAsync("session-1").Returns((Infographic?)null);
        _memos.GetBySessionIdAsync("session-1").Returns((Memo?)null);

        var result = await _sut.ProcessAsync("session-1");

        result.Should().BeTrue();
        await _infographicService.DidNotReceive().GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }
}
