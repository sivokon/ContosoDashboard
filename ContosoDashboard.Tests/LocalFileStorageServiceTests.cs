using ContosoDashboard.Services;

namespace ContosoDashboard.Tests;

public class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "contoso-storage-tests-" + Guid.NewGuid());
    private readonly LocalFileStorageService _storage;

    public LocalFileStorageServiceTests()
    {
        _storage = new LocalFileStorageService(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Upload_Download_Delete_RoundTrip()
    {
        var path = "4/personal/" + Guid.NewGuid() + ".pdf";
        var bytes = new byte[] { 1, 2, 3, 4, 5 };

        await _storage.UploadAsync(new MemoryStream(bytes), path, "application/pdf");

        await using (var stream = await _storage.DownloadAsync(path))
        {
            using var copy = new MemoryStream();
            await stream.CopyToAsync(copy);
            Assert.Equal(bytes, copy.ToArray());
        }

        await _storage.DeleteAsync(path);
        await Assert.ThrowsAsync<FileNotFoundException>(() => _storage.DownloadAsync(path));
    }

    [Fact]
    public async Task Delete_MissingFile_IsNoOp()
    {
        await _storage.DeleteAsync("4/personal/missing.pdf");
    }

    [Fact]
    public async Task Upload_ToExistingPath_Fails()
    {
        var path = "4/personal/" + Guid.NewGuid() + ".pdf";
        await _storage.UploadAsync(new MemoryStream(new byte[] { 1 }), path, "application/pdf");

        await Assert.ThrowsAsync<IOException>(() =>
            _storage.UploadAsync(new MemoryStream(new byte[] { 2 }), path, "application/pdf"));
    }

    [Theory]
    [InlineData("../escape.pdf")]
    [InlineData("4/../../escape.pdf")]
    [InlineData("C:\\Windows\\win.ini")]
    [InlineData("/etc/passwd")]
    [InlineData("")]
    public async Task Paths_Outside_Root_AreRejected(string path)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _storage.UploadAsync(new MemoryStream(new byte[] { 1 }), path, "application/pdf"));
        await Assert.ThrowsAsync<ArgumentException>(() => _storage.DownloadAsync(path));
        await Assert.ThrowsAsync<ArgumentException>(() => _storage.DeleteAsync(path));
    }
}
