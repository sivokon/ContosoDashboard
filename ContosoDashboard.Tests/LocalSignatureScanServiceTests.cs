using ContosoDashboard.Services;

namespace ContosoDashboard.Tests;

public class LocalSignatureScanServiceTests
{
    private readonly LocalSignatureScanService _scanner = new();

    [Fact]
    public async Task CleanContent_IsClean()
    {
        var result = await _scanner.ScanAsync(new MemoryStream(new byte[] { 1, 2, 3 }), "a.txt");
        Assert.Equal(MalwareScanStatus.Clean, result.Status);
    }

    [Fact]
    public async Task EicarString_IsInfected()
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(LocalSignatureScanService.EicarTestString);
        var result = await _scanner.ScanAsync(new MemoryStream(bytes), "a.txt");
        Assert.Equal(MalwareScanStatus.Infected, result.Status);
    }

    [Fact]
    public async Task EicarString_AcrossChunkBoundary_IsInfected()
    {
        var eicar = System.Text.Encoding.ASCII.GetBytes(LocalSignatureScanService.EicarTestString);
        var prefix = new byte[64 * 1024 - 10];
        var bytes = prefix.Concat(eicar).ToArray();

        var result = await _scanner.ScanAsync(new MemoryStream(bytes), "a.txt");
        Assert.Equal(MalwareScanStatus.Infected, result.Status);
    }

    [Fact]
    public async Task FailingStream_IsUnavailable()
    {
        var result = await _scanner.ScanAsync(new ThrowingStream(), "a.txt");
        Assert.Equal(MalwareScanStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task Stream_IsNotDisposed_AndPositionIsRestored()
    {
        var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        stream.Position = 2;

        await _scanner.ScanAsync(stream, "a.txt");

        Assert.True(stream.CanRead);
        Assert.Equal(2, stream.Position);
    }

    private sealed class ThrowingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("boom");
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw new IOException("boom");
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
