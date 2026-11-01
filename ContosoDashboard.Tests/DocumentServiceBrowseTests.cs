using ContosoDashboard.Models;
using ContosoDashboard.Services;
using ContosoDashboard.Tests.TestSupport;

namespace ContosoDashboard.Tests;

public class DocumentServiceBrowseTests
{
    private readonly DocumentTestHarness _h = new();

    private async Task<int> UploadAtAsync(int userId, string title, string category, long size, DateTime date, int? projectId = null)
    {
        var id = await _h.UploadOkAsync(userId, title: title, category: category,
            content: SampleFiles.Pdf((int)size), projectId: projectId);
        var doc = await _h.Context.Documents.FindAsync(id);
        doc!.UploadDate = date;
        await _h.Context.SaveChangesAsync();
        return id;
    }

    private Task<List<DocumentListItem>> MineAsync(int userId, Action<DocumentQuery>? configure = null)
    {
        var query = new DocumentQuery { Scope = DocumentScope.Mine };
        configure?.Invoke(query);
        return _h.Service.GetDocumentsAsync(query, userId);
    }

    [Fact]
    public async Task Mine_ReturnsOnlyCallersUploads()
    {
        await _h.UploadOkAsync(TestUsers.Employee, title: "Mine");
        await _h.UploadOkAsync(TestUsers.ProjectManager, title: "Not mine");

        var items = await MineAsync(TestUsers.Employee);

        var item = Assert.Single(items);
        Assert.Equal("Mine", item.Title);
    }

    [Fact]
    public async Task Mine_ReturnsListFields()
    {
        await _h.UploadOkAsync(TestUsers.Employee, title: "Plan", category: DocumentCategories.Reports,
            projectId: TestUsers.SampleProject, content: SampleFiles.Pdf(3000));

        var item = Assert.Single(await MineAsync(TestUsers.Employee));

        Assert.Equal("Plan", item.Title);
        Assert.Equal(DocumentCategories.Reports, item.Category);
        Assert.Equal(3000, item.FileSize);
        Assert.Equal(TestUsers.SampleProject, item.ProjectId);
        Assert.Equal("ContosoDashboard Development", item.ProjectName);
        Assert.Equal("Ni Kang", item.UploadedByName);
        Assert.True(item.CanEdit && item.CanDelete && item.CanReplace && item.CanShare);
        Assert.True(item.CanPreview);
    }

    [Fact]
    public async Task Sorting_WorksForEveryField_InBothDirections()
    {
        var now = DateTime.UtcNow;
        await UploadAtAsync(TestUsers.Employee, "Bravo", DocumentCategories.Reports, 2000, now.AddDays(-1));
        await UploadAtAsync(TestUsers.Employee, "Alpha", DocumentCategories.Other, 3000, now.AddDays(-3));
        await UploadAtAsync(TestUsers.Employee, "Charlie", DocumentCategories.Presentations, 1000, now.AddDays(-2));

        async Task<string[]> TitlesAsync(DocumentSortField field, bool desc) =>
            (await MineAsync(TestUsers.Employee, q => { q.SortBy = field; q.SortDescending = desc; }))
                .Select(i => i.Title).ToArray();

        Assert.Equal(new[] { "Alpha", "Bravo", "Charlie" }, await TitlesAsync(DocumentSortField.Title, false));
        Assert.Equal(new[] { "Charlie", "Bravo", "Alpha" }, await TitlesAsync(DocumentSortField.Title, true));

        Assert.Equal(new[] { "Alpha", "Charlie", "Bravo" }, await TitlesAsync(DocumentSortField.UploadDate, false));
        Assert.Equal(new[] { "Bravo", "Charlie", "Alpha" }, await TitlesAsync(DocumentSortField.UploadDate, true));

        Assert.Equal(new[] { "Alpha", "Charlie", "Bravo" }, await TitlesAsync(DocumentSortField.Category, false));
        Assert.Equal(new[] { "Bravo", "Charlie", "Alpha" }, await TitlesAsync(DocumentSortField.Category, true));

        Assert.Equal(new[] { "Charlie", "Bravo", "Alpha" }, await TitlesAsync(DocumentSortField.FileSize, false));
        Assert.Equal(new[] { "Alpha", "Bravo", "Charlie" }, await TitlesAsync(DocumentSortField.FileSize, true));
    }

    [Fact]
    public async Task DefaultSort_IsNewestFirst()
    {
        var now = DateTime.UtcNow;
        await UploadAtAsync(TestUsers.Employee, "Old", DocumentCategories.Other, 1000, now.AddDays(-5));
        await UploadAtAsync(TestUsers.Employee, "New", DocumentCategories.Other, 1000, now);

        var items = await MineAsync(TestUsers.Employee);

        Assert.Equal(new[] { "New", "Old" }, items.Select(i => i.Title).ToArray());
    }

    [Fact]
    public async Task Filters_ByCategoryProjectAndDateRange_AloneAndCombined()
    {
        var now = DateTime.UtcNow.Date;
        await UploadAtAsync(TestUsers.Employee, "A", DocumentCategories.Reports, 1000, now.AddDays(-10), TestUsers.SampleProject);
        await UploadAtAsync(TestUsers.Employee, "B", DocumentCategories.Reports, 1000, now.AddDays(-5));
        await UploadAtAsync(TestUsers.Employee, "C", DocumentCategories.Other, 1000, now.AddDays(-5), TestUsers.SampleProject);
        await UploadAtAsync(TestUsers.Employee, "D", DocumentCategories.Other, 1000, now.AddDays(-1));

        async Task<string[]> TitlesAsync(Action<DocumentQuery> configure) =>
            (await MineAsync(TestUsers.Employee, q => { q.SortBy = DocumentSortField.Title; q.SortDescending = false; configure(q); }))
                .Select(i => i.Title).ToArray();

        Assert.Equal(new[] { "A", "B" }, await TitlesAsync(q => q.Category = DocumentCategories.Reports));
        Assert.Equal(new[] { "A", "C" }, await TitlesAsync(q => q.ProjectId = TestUsers.SampleProject));
        Assert.Equal(new[] { "B", "C", "D" }, await TitlesAsync(q => q.FromDate = now.AddDays(-6)));
        Assert.Equal(new[] { "A", "B", "C" }, await TitlesAsync(q => q.ToDate = now.AddDays(-5)));
        Assert.Equal(new[] { "B", "C" }, await TitlesAsync(q => { q.FromDate = now.AddDays(-6); q.ToDate = now.AddDays(-2); }));
        Assert.Equal(new[] { "A" }, await TitlesAsync(q =>
        {
            q.Category = DocumentCategories.Reports;
            q.ProjectId = TestUsers.SampleProject;
            q.ToDate = now.AddDays(-7);
        }));
    }

    [Fact]
    public async Task NoMatches_ReturnsEmptyList()
    {
        await _h.UploadOkAsync(TestUsers.Employee, category: DocumentCategories.Reports);

        var items = await MineAsync(TestUsers.Employee, q => q.Category = DocumentCategories.Presentations);

        Assert.Empty(items);
    }

    [Fact]
    public async Task UnknownUser_GetsEmptyList()
    {
        Assert.Empty(await MineAsync(999));
    }
}
