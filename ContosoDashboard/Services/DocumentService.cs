using Microsoft.EntityFrameworkCore;
using ContosoDashboard.Data;
using ContosoDashboard.Models;

namespace ContosoDashboard.Services;

public interface IDocumentService
{
    // Upload and file lifecycle
    Task<DocumentOperationResult> UploadAsync(DocumentUploadRequest request, int requestingUserId);

    // Browse (always access-filtered)
    Task<List<DocumentListItem>> GetDocumentsAsync(DocumentQuery query, int requestingUserId);
    Task<DocumentListItem?> GetDocumentAsync(int documentId, int requestingUserId);

    // Content access (also records Download / Preview activity).
    // Both return null when the document does not exist or the user may not view it.
    // Both throw FileNotFoundException when the record exists but the stored file is missing.
    Task<DocumentContent?> OpenForDownloadAsync(int documentId, int requestingUserId);
    Task<DocumentContent?> OpenForPreviewAsync(int documentId, int requestingUserId);
}

public class DocumentService : IDocumentService
{
    private const int MaxTagCount = 10;
    private const int MaxTagLength = 50;

    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _storage;
    private readonly IMalwareScanService _scanner;
    private readonly INotificationService _notificationService;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        ApplicationDbContext context,
        IFileStorageService storage,
        IMalwareScanService scanner,
        INotificationService notificationService,
        ILogger<DocumentService> logger)
    {
        _context = context;
        _storage = storage;
        _scanner = scanner;
        _notificationService = notificationService;
        _logger = logger;
    }

    // ---------------------------------------------------------------------
    // Upload
    // ---------------------------------------------------------------------

    public async Task<DocumentOperationResult> UploadAsync(DocumentUploadRequest request, int requestingUserId)
    {
        var user = await LoadUserAsync(requestingUserId);
        if (user == null)
            return DocumentOperationResult.Fail("User not found.");

        var title = request.Title?.Trim() ?? string.Empty;
        if (title.Length == 0)
            return DocumentOperationResult.Fail("A title is required.");
        if (title.Length > 255)
            return DocumentOperationResult.Fail("The title must be 255 characters or fewer.");

        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        if (description != null && description.Length > 2000)
            return DocumentOperationResult.Fail("The description must be 2000 characters or fewer.");

        if (!DocumentCategories.All.Contains(request.Category))
            return DocumentOperationResult.Fail("Please choose a valid category.");

        var (tags, tagError) = NormalizeTags(request.Tags);
        if (tagError != null)
            return DocumentOperationResult.Fail(tagError);

        var content = request.Content;
        var length = content.CanSeek ? content.Length : request.ContentLength;
        var header = await ReadHeaderAsync(content);

        var validationError = DocumentFileValidator.Validate(request.OriginalFileName, length, header);
        if (validationError != null)
            return DocumentOperationResult.Fail(validationError);

        var extension = DocumentFileValidator.GetExtension(request.OriginalFileName);
        var fileType = DocumentFileValidator.GetMimeType(extension)!;

        if (request.ProjectId.HasValue && !await CanUploadToProjectAsync(user, request.ProjectId.Value))
            return DocumentOperationResult.Fail("You do not have permission to add documents to that project.");

        var scan = await ScanAsync(content, request.OriginalFileName);
        if (scan.Status == MalwareScanStatus.Infected)
            return DocumentOperationResult.Fail("The file was rejected because it failed the malware scan.");
        if (scan.Status == MalwareScanStatus.Unavailable)
            return DocumentOperationResult.Fail("The malware scan is unavailable. Please try again later.");

        // Path is generated before saving so the record never has an empty or duplicate path
        var storagePath = $"{user.UserId}/{(request.ProjectId?.ToString() ?? "personal")}/{Guid.NewGuid():D}{extension}";

        try
        {
            content.Position = 0;
            await _storage.UploadAsync(content, storagePath, fileType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store uploaded file for user {UserId}", user.UserId);
            return DocumentOperationResult.Fail("The file could not be saved. Please try again.");
        }

        var document = new Document
        {
            Title = title,
            Description = description,
            Category = request.Category,
            Tags = tags,
            OriginalFileName = Path.GetFileName(request.OriginalFileName),
            FilePath = storagePath,
            FileType = fileType,
            FileSize = length,
            UploadedByUserId = user.UserId,
            ProjectId = request.ProjectId,
            UploadDate = DateTime.UtcNow
        };

        try
        {
            _context.Documents.Add(document);
            LogActivity(document, user.UserId, DocumentActivityType.Upload, null);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save document record for user {UserId}", user.UserId);
            _context.Entry(document).State = EntityState.Detached;
            await TryDeleteFileAsync(storagePath);
            return DocumentOperationResult.Fail("The document could not be saved. Please try again.");
        }

        return DocumentOperationResult.Ok(document);
    }

    // ---------------------------------------------------------------------
    // Browse
    // ---------------------------------------------------------------------

    public async Task<List<DocumentListItem>> GetDocumentsAsync(DocumentQuery query, int requestingUserId)
    {
        var user = await LoadUserAsync(requestingUserId);
        if (user == null)
            return new List<DocumentListItem>();

        IQueryable<Document> documents = _context.Documents.AsNoTracking();

        switch (query.Scope)
        {
            case DocumentScope.Mine:
                documents = documents.Where(d => d.UploadedByUserId == user.UserId);
                break;
            default:
                return new List<DocumentListItem>();
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
            documents = documents.Where(d => d.Category == query.Category);

        if (query.ProjectId.HasValue)
            documents = documents.Where(d => d.ProjectId == query.ProjectId.Value);

        if (query.FromDate.HasValue)
        {
            var from = query.FromDate.Value.Date;
            documents = documents.Where(d => d.UploadDate >= from);
        }

        if (query.ToDate.HasValue)
        {
            var toExclusive = query.ToDate.Value.Date.AddDays(1);
            documents = documents.Where(d => d.UploadDate < toExclusive);
        }

        documents = ApplySort(documents, query.SortBy, query.SortDescending);

        return await ToListItemsAsync(documents, user);
    }

    public async Task<DocumentListItem?> GetDocumentAsync(int documentId, int requestingUserId)
    {
        var user = await LoadUserAsync(requestingUserId);
        if (user == null)
            return null;

        var documents = ApplyViewFilter(_context.Documents.AsNoTracking(), user)
            .Where(d => d.DocumentId == documentId);

        return (await ToListItemsAsync(documents, user)).FirstOrDefault();
    }

    // ---------------------------------------------------------------------
    // Content access
    // ---------------------------------------------------------------------

    public Task<DocumentContent?> OpenForDownloadAsync(int documentId, int requestingUserId) =>
        OpenAsync(documentId, requestingUserId, DocumentActivityType.Download);

    public Task<DocumentContent?> OpenForPreviewAsync(int documentId, int requestingUserId) =>
        OpenAsync(documentId, requestingUserId, DocumentActivityType.Preview);

    private async Task<DocumentContent?> OpenAsync(int documentId, int requestingUserId, DocumentActivityType activity)
    {
        var user = await LoadUserAsync(requestingUserId);
        if (user == null)
            return null;

        var document = await ApplyViewFilter(_context.Documents.AsNoTracking(), user)
            .FirstOrDefaultAsync(d => d.DocumentId == documentId);

        if (document == null)
            return null;

        if (activity == DocumentActivityType.Preview && !DocumentFileValidator.IsPreviewable(document.FileType))
            return null;

        Stream stream;
        try
        {
            stream = await _storage.DownloadAsync(document.FilePath);
        }
        catch (FileNotFoundException)
        {
            _logger.LogError("Stored file missing for document {DocumentId}", document.DocumentId);
            throw;
        }

        LogActivity(document, user.UserId, activity, null);
        await _context.SaveChangesAsync();

        return new DocumentContent(stream, document.OriginalFileName, document.FileType, document.FileSize);
    }

    // ---------------------------------------------------------------------
    // Authorization core
    // ---------------------------------------------------------------------

    // Documents the user may view: uploader, Administrator, project manager/member of the document's project,
    // Team Lead in the uploader's department (excluding Personal Files), or a share to the user/department.
    internal static IQueryable<Document> ApplyViewFilter(IQueryable<Document> query, User user)
    {
        if (user.Role == UserRole.Administrator)
            return query;

        var userId = user.UserId;
        var department = user.Department;
        var isTeamLead = user.Role == UserRole.TeamLead && !string.IsNullOrEmpty(department);
        var hasDepartment = !string.IsNullOrEmpty(department);

        return query.Where(d =>
            d.UploadedByUserId == userId
            || (d.ProjectId != null && d.Project != null
                && (d.Project.ProjectManagerId == userId
                    || d.Project.ProjectMembers.Any(pm => pm.UserId == userId)))
            || (isTeamLead
                && d.Category != DocumentCategories.PersonalFiles
                && d.UploadedBy.Department == department)
            || d.Shares.Any(s => s.SharedWithUserId == userId
                || (hasDepartment && s.SharedWithDepartment == department)));
    }

    private record DocumentPermissions(bool CanEdit, bool CanDelete, bool CanReplace, bool CanShare);

    // Edit/delete: uploader, Administrator, manager of the document's project. Replace and share: uploader only.
    private static DocumentPermissions GetPermissions(User user, int uploadedByUserId, int? projectManagerId)
    {
        var isOwner = uploadedByUserId == user.UserId;
        var isAdmin = user.Role == UserRole.Administrator;
        var isProjectManager = projectManagerId.HasValue && projectManagerId.Value == user.UserId;
        var canManage = isOwner || isAdmin || isProjectManager;

        return new DocumentPermissions(canManage, canManage, isOwner, isOwner);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private async Task<User?> LoadUserAsync(int userId) =>
        await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);

    // Adds the activity row without saving; callers save in the same SaveChangesAsync as the action
    private void LogActivity(Document document, int userId, DocumentActivityType type, string? details)
    {
        _context.DocumentActivities.Add(new DocumentActivity
        {
            DocumentId = document.DocumentId == 0 ? null : document.DocumentId,
            Document = document.DocumentId == 0 ? document : null,
            DocumentTitle = document.Title,
            FileType = document.FileType,
            UserId = userId,
            ActivityType = type,
            Details = details,
            ActivityDate = DateTime.UtcNow
        });
    }

    private async Task<bool> CanUploadToProjectAsync(User user, int projectId)
    {
        if (user.Role == UserRole.Administrator)
            return await _context.Projects.AnyAsync(p => p.ProjectId == projectId);

        return await _context.Projects.AnyAsync(p => p.ProjectId == projectId
            && (p.ProjectManagerId == user.UserId || p.ProjectMembers.Any(pm => pm.UserId == user.UserId)));
    }

    private async Task<MalwareScanResult> ScanAsync(Stream content, string fileName)
    {
        try
        {
            content.Position = 0;
            var result = await _scanner.ScanAsync(content, fileName);
            content.Position = 0;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Malware scan failed");
            return new MalwareScanResult(MalwareScanStatus.Unavailable, "The malware scan could not be completed.");
        }
    }

    private async Task TryDeleteFileAsync(string storagePath)
    {
        try
        {
            await _storage.DeleteAsync(storagePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete stored file {StoragePath}", storagePath);
        }
    }

    private static async Task<byte[]> ReadHeaderAsync(Stream content)
    {
        content.Position = 0;
        var buffer = new byte[DocumentFileValidator.HeaderBytesNeeded];
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await content.ReadAsync(buffer.AsMemory(total, buffer.Length - total));
            if (read == 0)
                break;
            total += read;
        }

        content.Position = 0;
        return buffer.AsSpan(0, total).ToArray();
    }

    private static (string? Tags, string? Error) NormalizeTags(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
            return (null, null);

        var parts = tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (parts.Count == 0)
            return (null, null);
        if (parts.Count > MaxTagCount)
            return (null, $"Use at most {MaxTagCount} tags.");
        if (parts.Any(t => t.Length > MaxTagLength))
            return (null, $"Each tag must be {MaxTagLength} characters or fewer.");

        return (string.Join(",", parts), null);
    }

    private static IQueryable<Document> ApplySort(IQueryable<Document> query, DocumentSortField sortBy, bool descending) =>
        (sortBy, descending) switch
        {
            (DocumentSortField.Title, false) => query.OrderBy(d => d.Title),
            (DocumentSortField.Title, true) => query.OrderByDescending(d => d.Title),
            (DocumentSortField.Category, false) => query.OrderBy(d => d.Category).ThenByDescending(d => d.UploadDate),
            (DocumentSortField.Category, true) => query.OrderByDescending(d => d.Category).ThenByDescending(d => d.UploadDate),
            (DocumentSortField.FileSize, false) => query.OrderBy(d => d.FileSize),
            (DocumentSortField.FileSize, true) => query.OrderByDescending(d => d.FileSize),
            (_, false) => query.OrderBy(d => d.UploadDate),
            _ => query.OrderByDescending(d => d.UploadDate)
        };

    // Projects to list items without loading file content and fills per-user permission flags
    private static async Task<List<DocumentListItem>> ToListItemsAsync(IQueryable<Document> documents, User user)
    {
        var rows = await documents
            .Select(d => new
            {
                d.DocumentId,
                d.Title,
                d.Description,
                d.Category,
                d.Tags,
                d.UploadDate,
                d.FileSize,
                d.FileType,
                d.OriginalFileName,
                d.UploadedByUserId,
                UploadedByName = d.UploadedBy.DisplayName,
                d.ProjectId,
                ProjectName = d.Project != null ? d.Project.Name : null,
                ProjectManagerId = d.Project != null ? (int?)d.Project.ProjectManagerId : null,
                d.TaskId
            })
            .ToListAsync();

        return rows.Select(r =>
        {
            var permissions = GetPermissions(user, r.UploadedByUserId, r.ProjectManagerId);
            return new DocumentListItem
            {
                DocumentId = r.DocumentId,
                Title = r.Title,
                Description = r.Description,
                Category = r.Category,
                Tags = r.Tags,
                UploadDate = r.UploadDate,
                FileSize = r.FileSize,
                FileType = r.FileType,
                OriginalFileName = r.OriginalFileName,
                UploadedByUserId = r.UploadedByUserId,
                UploadedByName = r.UploadedByName,
                ProjectId = r.ProjectId,
                ProjectName = r.ProjectName,
                TaskId = r.TaskId,
                CanEdit = permissions.CanEdit,
                CanDelete = permissions.CanDelete,
                CanReplace = permissions.CanReplace,
                CanShare = permissions.CanShare,
                CanPreview = DocumentFileValidator.IsPreviewable(r.FileType)
            };
        }).ToList();
    }
}
