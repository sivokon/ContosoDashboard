using Microsoft.EntityFrameworkCore;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using ContosoDashboard.Tests.TestSupport;

namespace ContosoDashboard.Tests;

public class DocumentServiceAccessTests
{
    private readonly DocumentTestHarness _h = new();

    private static async Task<byte[]> ReadAllAsync(DocumentContent content)
    {
        await using (content.Content)
        {
            using var copy = new MemoryStream();
            await content.Content.CopyToAsync(copy);
            return copy.ToArray();
        }
    }

    [Fact]
    public async Task Download_ReturnsBytesNameAndType_ForUploader()
    {
        var bytes = SampleFiles.Pdf(777);
        var id = await _h.UploadOkAsync(TestUsers.Employee, "Plan v1.pdf", bytes);

        var content = await _h.Service.OpenForDownloadAsync(id, TestUsers.Employee);

        Assert.NotNull(content);
        Assert.Equal("Plan v1.pdf", content!.FileName);
        Assert.Equal("application/pdf", content.ContentType);
        Assert.Equal(bytes.Length, content.Length);
        Assert.Equal(bytes, await ReadAllAsync(content));
    }

    [Fact]
    public async Task Download_ReturnsNull_ForUserWithoutAccess_AndLogsNothing()
    {
        var id = await _h.UploadOkAsync(TestUsers.Employee);
        var activitiesBefore = await _h.Context.DocumentActivities.CountAsync();

        var content = await _h.Service.OpenForDownloadAsync(id, TestUsers.EngineeringOutsider);

        Assert.Null(content);
        Assert.Equal(activitiesBefore, await _h.Context.DocumentActivities.CountAsync());
    }

    [Fact]
    public async Task Download_ReturnsNull_ForNonexistentId()
    {
        Assert.Null(await _h.Service.OpenForDownloadAsync(12345, TestUsers.Admin));
    }

    [Fact]
    public async Task Download_ReturnsNull_ForUnknownUser()
    {
        var id = await _h.UploadOkAsync(TestUsers.Employee);
        Assert.Null(await _h.Service.OpenForDownloadAsync(id, 999));
    }

    [Fact]
    public async Task Administrator_CanDownloadAnyDocument()
    {
        var id = await _h.UploadOkAsync(TestUsers.Employee, category: DocumentCategories.PersonalFiles);
        Assert.NotNull(await _h.Service.OpenForDownloadAsync(id, TestUsers.Admin));
    }

    [Theory]
    [InlineData("a.pdf")]
    [InlineData("a.png")]
    [InlineData("a.jpg")]
    public async Task Preview_ReturnsContent_ForPdfAndImages(string fileName)
    {
        var bytes = fileName.EndsWith(".pdf") ? SampleFiles.Pdf()
            : fileName.EndsWith(".png") ? SampleFiles.Png() : SampleFiles.Jpeg();
        var id = await _h.UploadOkAsync(TestUsers.Employee, fileName, bytes);

        var content = await _h.Service.OpenForPreviewAsync(id, TestUsers.Employee);

        Assert.NotNull(content);
        await content!.Content.DisposeAsync();
    }

    [Theory]
    [InlineData("a.docx")]
    [InlineData("a.txt")]
    public async Task Preview_ReturnsNull_ForOtherTypes(string fileName)
    {
        var bytes = fileName.EndsWith(".docx") ? SampleFiles.Docx() : SampleFiles.Text();
        var id = await _h.UploadOkAsync(TestUsers.Employee, fileName, bytes);

        Assert.Null(await _h.Service.OpenForPreviewAsync(id, TestUsers.Employee));
    }

    [Fact]
    public async Task Preview_ReturnsNull_WithoutAccess()
    {
        var id = await _h.UploadOkAsync(TestUsers.Employee);
        Assert.Null(await _h.Service.OpenForPreviewAsync(id, TestUsers.EngineeringOutsider));
    }

    [Fact]
    public async Task Activity_IsRecorded_OnlyOnSuccess()
    {
        var id = await _h.UploadOkAsync(TestUsers.Employee);

        (await _h.Service.OpenForDownloadAsync(id, TestUsers.Employee))!.Content.Dispose();
        (await _h.Service.OpenForPreviewAsync(id, TestUsers.Employee))!.Content.Dispose();
        await _h.Service.OpenForDownloadAsync(id, TestUsers.EngineeringOutsider);

        var types = await _h.Context.DocumentActivities.OrderBy(a => a.DocumentActivityId)
            .Select(a => a.ActivityType).ToListAsync();
        Assert.Equal(
            new[] { DocumentActivityType.Upload, DocumentActivityType.Download, DocumentActivityType.Preview },
            types.ToArray());
    }

    [Fact]
    public async Task MissingStoredFile_ThrowsFileNotFound()
    {
        var id = await _h.UploadOkAsync(TestUsers.Employee);
        _h.Storage.Files.Clear();

        await Assert.ThrowsAsync<FileNotFoundException>(() => _h.Service.OpenForDownloadAsync(id, TestUsers.Employee));
    }

    [Fact]
    public async Task GetDocument_ReturnsItem_OnlyWhenViewable()
    {
        var id = await _h.UploadOkAsync(TestUsers.Employee);

        Assert.NotNull(await _h.Service.GetDocumentAsync(id, TestUsers.Employee));
        Assert.NotNull(await _h.Service.GetDocumentAsync(id, TestUsers.Admin));
        Assert.Null(await _h.Service.GetDocumentAsync(id, TestUsers.EngineeringOutsider));
        Assert.Null(await _h.Service.GetDocumentAsync(999, TestUsers.Admin));
    }
}
