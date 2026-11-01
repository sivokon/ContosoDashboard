using ContosoDashboard.Services;

namespace ContosoDashboard.Tests.TestSupport;

public class FakeFileStorageService : IFileStorageService
{
    public Dictionary<string, byte[]> Files { get; } = new();
    public bool ThrowOnUpload { get; set; }
    public bool ThrowOnDelete { get; set; }
    public List<string> DeletedPaths { get; } = new();

    public async Task<string> UploadAsync(Stream content, string storagePath, string contentType, CancellationToken ct = default)
    {
        if (ThrowOnUpload)
            throw new IOException("Simulated storage failure.");

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        Files[storagePath] = buffer.ToArray();
        return storagePath;
    }

    public Task DeleteAsync(string storagePath, CancellationToken ct = default)
    {
        if (ThrowOnDelete)
            throw new IOException("Simulated delete failure.");

        Files.Remove(storagePath);
        DeletedPaths.Add(storagePath);
        return Task.CompletedTask;
    }

    public Task<Stream> DownloadAsync(string storagePath, CancellationToken ct = default)
    {
        if (!Files.TryGetValue(storagePath, out var bytes))
            throw new FileNotFoundException("Missing in fake storage.");

        return Task.FromResult<Stream>(new MemoryStream(bytes));
    }

    public Task<string> GetUrlAsync(string storagePath, TimeSpan expiration) => Task.FromResult(storagePath);
}

public class FakeMalwareScanService : IMalwareScanService
{
    public MalwareScanStatus Status { get; set; } = MalwareScanStatus.Clean;
    public int ScanCount { get; private set; }

    public Task<MalwareScanResult> ScanAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        ScanCount++;
        return Task.FromResult(new MalwareScanResult(Status, Status == MalwareScanStatus.Clean ? null : "fake"));
    }
}

public static class SampleFiles
{
    public static byte[] Pdf(int size = 1024) => WithHeader(new byte[] { 0x25, 0x50, 0x44, 0x46 }, size);

    public static byte[] Png(int size = 1024) =>
        WithHeader(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, size);

    public static byte[] Jpeg(int size = 1024) => WithHeader(new byte[] { 0xFF, 0xD8, 0xFF }, size);

    public static byte[] Docx(int size = 1024) => WithHeader(new byte[] { 0x50, 0x4B, 0x03, 0x04 }, size);

    public static byte[] Doc(int size = 1024) =>
        WithHeader(new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }, size);

    public static byte[] Text(string text = "hello") => System.Text.Encoding.UTF8.GetBytes(text);

    public static byte[] Executable(int size = 1024) => WithHeader(new byte[] { 0x4D, 0x5A }, size);

    private static byte[] WithHeader(byte[] header, int size)
    {
        var bytes = new byte[Math.Max(size, header.Length)];
        Array.Copy(header, bytes, header.Length);
        for (var i = header.Length; i < bytes.Length; i++)
            bytes[i] = (byte)('a' + i % 26);
        return bytes;
    }
}
