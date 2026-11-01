namespace ContosoDashboard.Services;

public enum DocumentScope
{
    Mine,
    SharedWithMe,
    Accessible
}

public enum DocumentSortField
{
    UploadDate,
    Title,
    Category,
    FileSize
}

public class DocumentQuery
{
    public DocumentScope Scope { get; set; } = DocumentScope.Mine;
    public string? SearchText { get; set; }
    public string? Category { get; set; }
    public int? ProjectId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public DocumentSortField SortBy { get; set; } = DocumentSortField.UploadDate;
    public bool SortDescending { get; set; } = true;
}

public class DocumentListItem
{
    public int DocumentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Tags { get; set; }
    public DateTime UploadDate { get; set; }
    public long FileSize { get; set; }
    public string FileType { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public int UploadedByUserId { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
    public int? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public int? TaskId { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public bool CanShare { get; set; }
    public bool CanReplace { get; set; }
    public bool CanPreview { get; set; }
}

public class DocumentUploadRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Tags { get; set; }
    public int? ProjectId { get; set; }
    public int? TaskId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public long ContentLength { get; set; }
    public Stream Content { get; set; } = Stream.Null;
}

public class DocumentOperationResult
{
    public bool Succeeded { get; private set; }
    public string? Error { get; private set; }
    public Models.Document? Document { get; private set; }

    public static DocumentOperationResult Ok(Models.Document? document = null) =>
        new() { Succeeded = true, Document = document };

    public static DocumentOperationResult Fail(string error) =>
        new() { Succeeded = false, Error = error };
}

public record DocumentContent(Stream Content, string FileName, string ContentType, long Length);

public class DocumentShareInfo
{
    public int DocumentShareId { get; set; }
    public int DocumentId { get; set; }
    public int? SharedWithUserId { get; set; }
    public string? SharedWithUserName { get; set; }
    public string? SharedWithDepartment { get; set; }
    public DateTime SharedDate { get; set; }
}

public record UploadTypeReportRow(string FileType, int UploadCount);

public record TopUploaderReportRow(int UserId, string UserName, int UploadCount);

public record AccessPatternReportRow(string DocumentTitle, DateTime Date, int DownloadCount, int PreviewCount);
