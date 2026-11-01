namespace ContosoDashboard.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootPath;

    public LocalFileStorageService(IConfiguration configuration, IWebHostEnvironment environment)
        : this(ResolveRoot(configuration, environment.ContentRootPath))
    {
    }

    public LocalFileStorageService(string rootPath)
    {
        _rootPath = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(_rootPath);
    }

    public string RootPath => _rootPath;

    public static string ResolveRoot(IConfiguration configuration, string contentRootPath)
    {
        var configured = configuration["FileStorage:RootPath"];
        if (string.IsNullOrWhiteSpace(configured))
            configured = "AppData/uploads";

        return Path.IsPathRooted(configured)
            ? Path.GetFullPath(configured)
            : Path.GetFullPath(Path.Combine(contentRootPath, configured));
    }

    public async Task<string> UploadAsync(Stream content, string storagePath, string contentType, CancellationToken ct = default)
    {
        var fullPath = ResolveFullPath(storagePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        // CreateNew fails if the file already exists, so nothing is overwritten
        await using var target = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(target, ct);

        return storagePath;
    }

    public Task DeleteAsync(string storagePath, CancellationToken ct = default)
    {
        var fullPath = ResolveFullPath(storagePath);
        if (File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }

    public Task<Stream> DownloadAsync(string storagePath, CancellationToken ct = default)
    {
        var fullPath = ResolveFullPath(storagePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("The stored file was not found.");

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Task.FromResult(stream);
    }

    public Task<string> GetUrlAsync(string storagePath, TimeSpan expiration)
    {
        ResolveFullPath(storagePath);
        return Task.FromResult(storagePath);
    }

    // Rejects rooted paths and anything that resolves outside the storage root
    private string ResolveFullPath(string storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath) || Path.IsPathRooted(storagePath))
            throw new ArgumentException("Invalid storage path.", nameof(storagePath));

        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, storagePath));
        var rootWithSeparator = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Invalid storage path.", nameof(storagePath));

        return fullPath;
    }
}
