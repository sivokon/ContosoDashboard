using Microsoft.EntityFrameworkCore;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using ContosoDashboard.Tests.TestSupport;

namespace ContosoDashboard.Tests;

public class DocumentServiceUploadTests
{
    private readonly DocumentTestHarness _h = new();

    [Fact]
    public async Task ValidUpload_StoresFileAndRecord()
    {
        var bytes = SampleFiles.Pdf(2048);

        var result = await _h.UploadAsync(TestUsers.Employee, "My Report.pdf", bytes, title: "  Report  ",
            description: "desc", tags: "alpha, Beta,alpha");

        Assert.True(result.Succeeded, result.Error);
        var doc = await _h.Context.Documents.SingleAsync();
        Assert.Equal("Report", doc.Title);
        Assert.Equal("desc", doc.Description);
        Assert.Equal("alpha,Beta", doc.Tags);
        Assert.Equal(TestUsers.Employee, doc.UploadedByUserId);
        Assert.Equal(bytes.Length, doc.FileSize);
        Assert.Equal("application/pdf", doc.FileType);
        Assert.Equal("My Report.pdf", doc.OriginalFileName);
        Assert.True((DateTime.UtcNow - doc.UploadDate).TotalMinutes < 1);

        Assert.StartsWith($"{TestUsers.Employee}/personal/", doc.FilePath);
        Assert.EndsWith(".pdf", doc.FilePath);
        Assert.True(Guid.TryParse(Path.GetFileNameWithoutExtension(doc.FilePath), out _));
        Assert.Equal(bytes, _h.Storage.Files[doc.FilePath]);

        var activity = await _h.Context.DocumentActivities.SingleAsync();
        Assert.Equal(DocumentActivityType.Upload, activity.ActivityType);
        Assert.Equal(TestUsers.Employee, activity.UserId);
        Assert.Equal(doc.DocumentId, activity.DocumentId);
    }

    [Fact]
    public async Task ProjectUpload_UsesProjectIdInPath()
    {
        var id = await _h.UploadOkAsync(TestUsers.Employee, projectId: TestUsers.SampleProject);
        var doc = await _h.Context.Documents.FindAsync(id);
        Assert.StartsWith($"{TestUsers.Employee}/{TestUsers.SampleProject}/", doc!.FilePath);
    }

    [Fact]
    public async Task OverSizedFile_IsRejected_WithNothingStored()
    {
        var bytes = SampleFiles.Pdf(25 * 1024 * 1024 + 1);
        var result = await _h.UploadAsync(TestUsers.Employee, content: bytes);

        Assert.False(result.Succeeded);
        Assert.Contains("25 MB", result.Error);
        AssertNothingStored();
    }

    [Fact]
    public async Task EmptyFile_IsRejected()
    {
        var result = await _h.UploadAsync(TestUsers.Employee, content: Array.Empty<byte>());

        Assert.False(result.Succeeded);
        AssertNothingStored();
    }

    [Fact]
    public async Task UnsupportedExtension_IsRejected()
    {
        var result = await _h.UploadAsync(TestUsers.Employee, "tool.exe", SampleFiles.Executable());

        Assert.False(result.Succeeded);
        Assert.Contains("Unsupported", result.Error);
        AssertNothingStored();
    }

    [Fact]
    public async Task SignatureMismatch_IsRejected()
    {
        var result = await _h.UploadAsync(TestUsers.Employee, "tool.pdf", SampleFiles.Executable());

        Assert.False(result.Succeeded);
        AssertNothingStored();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankTitle_IsRejected(string title)
    {
        var result = await _h.UploadAsync(TestUsers.Employee, title: title);

        Assert.False(result.Succeeded);
        AssertNothingStored();
    }

    [Fact]
    public async Task InvalidCategory_IsRejected()
    {
        var result = await _h.UploadAsync(TestUsers.Employee, category: "Secrets");

        Assert.False(result.Succeeded);
        AssertNothingStored();
    }

    [Fact]
    public async Task TooManyTags_IsRejected()
    {
        var tags = string.Join(",", Enumerable.Range(1, 11).Select(i => "t" + i));
        var result = await _h.UploadAsync(TestUsers.Employee, tags: tags);

        Assert.False(result.Succeeded);
        AssertNothingStored();
    }

    [Fact]
    public async Task InfectedFile_IsRejected_WithNothingStored()
    {
        _h.Scanner.Status = MalwareScanStatus.Infected;

        var result = await _h.UploadAsync(TestUsers.Employee);

        Assert.False(result.Succeeded);
        Assert.Contains("malware", result.Error, StringComparison.OrdinalIgnoreCase);
        AssertNothingStored();
    }

    [Fact]
    public async Task UnavailableScanner_RejectsWithTryAgainLater_AndStoresNothing()
    {
        _h.Scanner.Status = MalwareScanStatus.Unavailable;

        var result = await _h.UploadAsync(TestUsers.Employee);

        Assert.False(result.Succeeded);
        Assert.Contains("try again later", result.Error, StringComparison.OrdinalIgnoreCase);
        AssertNothingStored();
    }

    [Fact]
    public async Task StorageFailure_CreatesNoRecord()
    {
        _h.Storage.ThrowOnUpload = true;

        var result = await _h.UploadAsync(TestUsers.Employee);

        Assert.False(result.Succeeded);
        AssertNothingStored();
    }

    [Fact]
    public async Task DatabaseFailure_DeletesTheSavedFile()
    {
        _h.Context.FailOnSave = true;

        var result = await _h.UploadAsync(TestUsers.Employee);

        Assert.False(result.Succeeded);
        Assert.Empty(_h.Storage.Files);
        Assert.Single(_h.Storage.DeletedPaths);
    }

    [Fact]
    public async Task ProjectMember_CanUploadToProject()
    {
        var result = await _h.UploadAsync(TestUsers.Employee, projectId: TestUsers.SampleProject);
        Assert.True(result.Succeeded, result.Error);
    }

    [Fact]
    public async Task ProjectManager_CanUploadToOwnProject()
    {
        var result = await _h.UploadAsync(TestUsers.ProjectManager, projectId: TestUsers.SampleProject);
        Assert.True(result.Succeeded, result.Error);
    }

    [Fact]
    public async Task Administrator_CanUploadToAnyProject()
    {
        var result = await _h.UploadAsync(TestUsers.Admin, projectId: TestUsers.SampleProject);
        Assert.True(result.Succeeded, result.Error);
    }

    [Fact]
    public async Task NonMember_CannotUploadToProject()
    {
        var result = await _h.UploadAsync(TestUsers.EngineeringOutsider, projectId: TestUsers.SampleProject);

        Assert.False(result.Succeeded);
        AssertNothingStored();
    }

    [Fact]
    public async Task UnknownProject_IsRejected()
    {
        var result = await _h.UploadAsync(TestUsers.Admin, projectId: 999);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task UserSuppliedFileName_NeverAppearsInStoragePath()
    {
        var id = await _h.UploadOkAsync(TestUsers.Employee, "..\\..\\evil name.pdf");
        var doc = await _h.Context.Documents.FindAsync(id);

        Assert.DoesNotContain("evil", doc!.FilePath);
        Assert.DoesNotContain("..", doc.FilePath);
        Assert.Equal("evil name.pdf", doc.OriginalFileName);
    }

    [Fact]
    public async Task UnknownUser_IsRejected()
    {
        var result = await _h.UploadAsync(999);
        Assert.False(result.Succeeded);
    }

    private void AssertNothingStored()
    {
        Assert.Empty(_h.Context.Documents);
        Assert.Empty(_h.Context.DocumentActivities);
        Assert.Empty(_h.Storage.Files);
    }
}
