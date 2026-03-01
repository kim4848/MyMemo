using FluentAssertions;
using Microsoft.Extensions.Options;
using MyMemo.Shared.Services;

namespace MyMemo.Shared.Tests.Services;

public class WhisperServiceTests
{
    [Fact]
    public async Task TranscribeAsync_BuffersNonSeekableStream_IntoSeekableMemoryStream()
    {
        var data = new byte[] { 0x1A, 0x45, 0xDF, 0xA3, 0x01, 0x02, 0x03 };
        var nonSeekableStream = new ForwardOnlyStream(data);
        var service = new SpyWhisperService();

        await service.TranscribeAsync(nonSeekableStream, "da");

        service.CapturedStream.Should().NotBeNull();
        service.CapturedStream!.CanSeek.Should().BeTrue();
        service.CapturedStream.Position.Should().Be(0);
        service.CapturedStream.Length.Should().Be(data.Length);
    }

    [Fact]
    public async Task TranscribeAsync_BufferedStream_ContainsOriginalData()
    {
        var data = new byte[] { 0x1A, 0x45, 0xDF, 0xA3, 0x01, 0x02, 0x03 };
        var nonSeekableStream = new ForwardOnlyStream(data);
        var service = new SpyWhisperService();

        await service.TranscribeAsync(nonSeekableStream, "da");

        var buf = new byte[data.Length];
        await service.CapturedStream!.ReadAsync(buf);
        buf.Should().BeEquivalentTo(data);
    }

    [Fact]
    public async Task TranscribeAsync_BufferedStream_CanBeRereadForRetry()
    {
        var data = new byte[] { 0x1A, 0x45, 0xDF, 0xA3, 0x01, 0x02, 0x03 };
        var nonSeekableStream = new ForwardOnlyStream(data);
        var service = new SpyWhisperService();

        await service.TranscribeAsync(nonSeekableStream, "da");

        // First read
        var buf = new byte[data.Length];
        await service.CapturedStream!.ReadAsync(buf);
        buf.Should().BeEquivalentTo(data);

        // Simulate SDK retry: seek back and read again
        service.CapturedStream.Position = 0;
        var retryBuf = new byte[data.Length];
        await service.CapturedStream.ReadAsync(retryBuf);
        retryBuf.Should().BeEquivalentTo(data);
    }

    /// <summary>
    /// Testable subclass that captures the buffered stream instead of calling Azure.
    /// </summary>
    private sealed class SpyWhisperService : WhisperService
    {
        public MemoryStream? CapturedStream { get; private set; }

        public SpyWhisperService()
            : base(Options.Create(new AzureOpenAIOptions
            {
                Endpoint = "https://fake.openai.azure.com/",
                ApiKey = "fake-key",
                WhisperDeployment = "whisper"
            })) { }

        protected override Task<WhisperResult> TranscribeBufferedAsync(MemoryStream audioStream, string language)
        {
            CapturedStream = audioStream;
            return Task.FromResult(new WhisperResult("mocked", null, null));
        }
    }

    /// <summary>
    /// Stream wrapper that simulates a non-seekable blob download stream.
    /// </summary>
    private sealed class ForwardOnlyStream(byte[] data) : Stream
    {
        private readonly MemoryStream _inner = new(data);

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => _inner.Position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            _inner.Read(buffer, offset, count);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _inner.ReadAsync(buffer, offset, count, cancellationToken);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
