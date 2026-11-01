using Microsoft.Extensions.Logging.Abstractions;
using ContosoDashboard.Models;
using ContosoDashboard.Services;

namespace ContosoDashboard.Tests.TestSupport;

public class DocumentTestHarness
{
    public DocumentTestHarness()
    {
        Context = TestDb.Create();
        Storage = new FakeFileStorageService();
        Scanner = new FakeMalwareScanService();
        Service = new DocumentService(
            Context,
            Storage,
            Scanner,
            new NotificationService(Context),
            NullLogger<DocumentService>.Instance);
    }

    public TestDbContext Context { get; }
    public FakeFileStorageService Storage { get; }
    public FakeMalwareScanService Scanner { get; }
    public DocumentService Service { get; }

    public Task<DocumentOperationResult> UploadAsync(
        int userId,
        string fileName = "report.pdf",
        byte[]? content = null,
        string title = "Quarterly report",
        string category = DocumentCategories.Reports,
        int? projectId = null,
        string? tags = null,
        string? description = null)
    {
        content ??= SampleFiles.Pdf();
        return Service.UploadAsync(new DocumentUploadRequest
        {
            Title = title,
            Description = description,
            Category = category,
            Tags = tags,
            ProjectId = projectId,
            OriginalFileName = fileName,
            ContentLength = content.Length,
            Content = new MemoryStream(content)
        }, userId);
    }

    // Uploads and returns the stored document id; fails the test run loudly if the upload is rejected
    public async Task<int> UploadOkAsync(
        int userId,
        string fileName = "report.pdf",
        byte[]? content = null,
        string title = "Quarterly report",
        string category = DocumentCategories.Reports,
        int? projectId = null,
        string? tags = null,
        string? description = null)
    {
        var result = await UploadAsync(userId, fileName, content, title, category, projectId, tags, description);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Setup upload failed: {result.Error}");

        return result.Document!.DocumentId;
    }
}
