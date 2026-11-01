# Research: Document Upload and Management

All Technical Context items are resolved; no `NEEDS CLARIFICATION` remain. Findings below come from reading the existing code (`Program.cs`, `ApplicationDbContext`, services, pages) and the stakeholder document.

## R1. Authorization model and where it lives

- **Decision**: One `DocumentService` owns all access decisions. Every public method takes the acting `userId`, loads that user (role, department) from the database, and authorizes before touching data. A single composable query filter (`ApplyViewFilter`) defines "documents this user may view" and is reused by list, search, recent, count, download and preview so the rules cannot diverge. Edit, replace, delete and share rights are evaluated per document by one permissions function.
- **Rationale**: Matches the existing pattern (`ProjectService`, `TaskService` take `requestingUserId` and check in the service) and Constitution III (defense in depth, IDOR). Loading the role and department from the database rather than trusting claims keeps authorization correct if a role changes mid-session.
- **Alternatives considered**: ASP.NET resource-based `IAuthorizationHandler` (more framework surface, harder to reuse for list filtering); trusting role/department claims (stale, and the current login does not issue a Department claim).

### Permission matrix (derived from spec FR-011..FR-031 and clarifications)

| Action | Uploader | Administrator | Project Manager of doc's project | Project member | Team Lead (same department as uploader) | Share recipient |
|---|---|---|---|---|---|---|
| View / preview / download | Yes | Yes | Yes | Yes | Yes, except `Personal Files` | Yes |
| Edit metadata | Yes | Yes | Yes | No | No | No |
| Replace file | Yes | No | No | No | No | No |
| Delete | Yes | Yes | Yes | No | No | No |
| Share / revoke | Yes | No | No | No | No | No |

- Upload to a project requires being that project's manager, a member of it, or an Administrator.
- "Project Manager of a document's project" and "project member" come from `Project.ProjectManagerId` and `ProjectMember`, not from the global role. A user with the `ProjectManager` role who does not manage the project gets no extra rights.
- A Team Lead is a user whose `Role == TeamLead`; "team" is the same non-null `Department` as the uploader (spec assumption).
- Project membership grants view to every document with that `ProjectId`, regardless of category (spec FR-015). A user choosing a project for a "Personal Files" document is treated as intending to share it with that project. This was left open in the spec clarification summary and is recorded here as the chosen interpretation.
- Deviations from the spec text: FR-029 says Project Managers "manage" project documents; this plan reads that as view, edit metadata and delete (replace stays with the uploader, who is the one holding the source file).

## R2. File storage abstraction and layout

- **Decision**: `IFileStorageService` with `UploadAsync`, `DeleteAsync`, `DownloadAsync`, `GetUrlAsync` (names from the stakeholder document). `LocalFileStorageService` writes under a configurable root (`FileStorage:RootPath`, default `AppData/uploads` relative to the content root, resolved to an absolute path at startup). The service layer generates the relative storage path `{userId}/{projectId|personal}/{guid}.{ext}` and passes it in; the database stores that relative path.
- **Rationale**: Satisfies Constitution II and the stakeholder "generate unique path first" sequence. A relative, forward-slash path ports unchanged to Azure Blob names. The extension comes from a whitelist, never from user text, so the user-supplied filename never touches a path.
- **Defense in depth**: after combining root and relative path, `LocalFileStorageService` resolves the full path and rejects anything that does not stay inside the root (traversal guard), even though paths are server generated.
- **`GetUrlAsync` locally**: all local access goes through authorized endpoints, so the local implementation returns the relative storage path (not a public URL) and documents that it is not directly browsable; an Azure implementation would return a time-limited SAS URL. No caller in this feature uses it for links.
- **Alternatives considered**: storing under `wwwroot` (rejected: bypasses authorization); returning the storage path from `UploadAsync` (kept as the return value for symmetry with the README interface, but the caller already knows it).

## R3. Upload sequence and failure handling

- **Decision**: validate (extension whitelist, size, non-empty, signature/magic bytes) -> authorize (project membership) -> malware scan -> generate GUID path -> save file -> insert `Document` and `DocumentActivity` in one `SaveChanges`. If the insert fails, delete the saved file (compensating action). Replace saves the new file first, updates the record, then deletes the old file. Delete removes the database rows (document, shares) in one save, then deletes the file; if file deletion fails it is logged and the unreachable orphan file is tolerated.
- **Rationale**: Prevents orphaned records (FR-009) and duplicate-key issues from empty paths; an orphan file is harmless because nothing references it, while a record with no file would break users.
- **Alternatives considered**: transaction spanning file and DB (not possible across filesystem and SQL Server); soft delete (out of scope).

## R4. Validation: file type and content

- **Decision**: Whitelist by extension: `.pdf .doc .docx .xls .xlsx .ppt .pptx .txt .jpg .jpeg .png`. Server-side MIME type is derived from the extension (never trusting the browser `Content-Type`) and stored in `FileType` (max 255). Magic bytes are checked against the extension: `%PDF`, OOXML `PK\x03\x04`, legacy Office OLE header `D0 CF 11 E0 A1 B1 1A E1`, JPEG `FF D8 FF`, PNG `89 50 4E 47 0D 0A 1A 0A`; `.txt` must contain no NUL bytes. Zero-byte and over-25 MB (25 * 1024 * 1024 bytes) files are rejected.
- **Rationale**: Catches renamed executables and misleading extensions (spec edge case) without any dependency.
- **Alternatives considered**: trusting `IBrowserFile.ContentType` (spoofable); full content sniffing library (new dependency, not justified).

## R5. Malware scanning offline

- **Decision**: `IMalwareScanService.ScanAsync(Stream, fileName)` returns a result (`Clean`, `Infected` with a reason, or throws/returns `Unavailable`). `LocalSignatureScanService` is the training implementation: it detects the standard EICAR test string and returns `Infected`; any exception or timeout is reported as unavailable. The upload treats `Infected` as a rejection with a reason and `Unavailable` as a rejection with "try again later", storing nothing (spec FR-008, clarification 5, fail-closed).
- **Rationale**: Gives students a testable scan step offline (the EICAR string is harmless and standard) and a clean seam to swap in a real scanner (for example Microsoft Defender or a cloud service) later.
- **Alternatives considered**: no scanner (violates FR-008); shelling out to Windows Defender `MpCmdRun` (platform-specific, not portable, not testable).

## R6. Serving files and preview

- **Decision**: Minimal API endpoints `GET /api/documents/{id}/download` and `GET /api/documents/{id}/preview`, both `RequireAuthorization()`. They call `IDocumentService`; no access -> `404` (does not reveal existence, spec FR-031). Download sends `attachment` with the original filename. Preview sends `inline` only for PDF/JPEG/PNG and returns `400` for other types. Both log a `Download` or `Preview` activity (see R8). Existing `X-Content-Type-Options: nosniff` applies. PDFs open in a new browser tab, not an iframe, because the app sends `X-Frame-Options: DENY`; images render with `<img>` (allowed by `img-src 'self'`).
- **Rationale**: Files live outside `wwwroot` so they need an authorized handler; Blazor components cannot return file streams. Not weakening the existing security headers satisfies Constitution III.
- **Alternatives considered**: Razor Pages handler or MVC controllers (would need `AddControllers`, more ceremony); base64 data URLs in Blazor (memory heavy, bypasses audit).

## R7. Blazor Server upload mechanics

- **Decision**: `InputFile` with `Multiple`, `@key` rotated after a successful upload, `OpenReadStream(maxAllowedSize: 25 MB)`. Capture name/size/content type into locals first, copy to a `MemoryStream` in chunks (updating a progress percentage and calling `StateHasChanged`), clear the `IBrowserFile` reference, then call the service. Files in a batch are processed sequentially with per-file result messages.
- **Rationale**: Follows the stakeholder Blazor guidance and avoids stream-disposal errors; chunked copy gives a real progress indicator (FR-004). `InputFile` streams in small chunks over the SignalR circuit, so the default 32 KB hub message limit does not need changing; this is re-verified in quickstart with a 25 MB file.
- **Alternatives considered**: HTTP multipart POST to an endpoint (real network-level progress, but needs antiforgery handling and more plumbing; not needed at training scale); raising `MaximumReceiveMessageSize` (not required).

## R8. Activity logging and reports

- **Decision**: `DocumentActivity` rows record Upload, Download, Preview, EditMetadata, ReplaceFile, Delete, Share, RevokeShare with the actor, document id (nullable, set null on delete), a title snapshot, and a file type snapshot. `DocumentReportService` (Administrator only, enforced in the service) returns: uploads by file type, top uploaders (by upload count), and access patterns (downloads and previews per document and per day). Snapshots keep reports and the audit trail correct after a document is deleted.
- **Rationale**: Spec FR-036/FR-037 and clarification 3 (admin actions logged). Previews are logged together with downloads as "access".
- **Alternatives considered**: logging to files (not queryable for reports); foreign key with cascade delete (would erase audit history when a document is deleted).

## R9. Sharing

- **Decision**: `DocumentShare` targets either one user (`SharedWithUserId`) or a department (`SharedWithDepartment`, the "team" per the spec assumption); exactly one is set. Only the uploader shares and revokes. Sharing creates `Notification` rows (new `NotificationType.DocumentShared`) for the user, or for every user in the department except the sharer. A department share is evaluated live, so people joining the department later also gain access (documented behavior). "Shared with Me" lists documents with a share matching the user id or the user's department, excluding the user's own uploads.
- **Rationale**: Reuses the existing `INotificationService`; no new "team" entity is needed because none exists.
- **Alternatives considered**: expanding a team share into per-user rows at share time (stale as membership changes; revocation is more complex).

## R10. Task and dashboard integration

- **Decision**: No task detail page exists today (`Tasks.razor` has a TODO). Add `TaskDetails.razor` at `/tasks/{taskId:int}` with a Documents section (list, upload, attach existing). `Document.TaskId` is nullable; when a task is linked the document's `ProjectId` is set to the task's project (spec FR-032) if the task has one. Attaching an existing document requires the acting user to be its uploader and to be allowed to view the task (`TaskService.GetTaskByIdAsync`). `DashboardService`/`IDocumentService` provide the document count (documents the user uploaded) and the last 5 uploads. Add `NotificationType.DocumentAdded` for "new document in your project".
- **Rationale**: Smallest change that realizes FR-032..FR-035; reuses existing task authorization.
- **Alternatives considered**: a modal on `Tasks.razor` (that page is a list with a stubbed handler; a routed page is simpler to test and link to).

## R11. Search and performance

- **Decision**: Search runs `Contains` over title, description, tags, uploader display name, and project name within the already access-filtered query. Add indexes on `Document(UploadedByUserId)`, `(ProjectId)`, `(UploadDate)`, `(Category)`, and `DocumentShare(SharedWithUserId)`, `(SharedWithDepartment)`, `(DocumentId)`. Lists are projected to a lightweight view model (no file bytes) and paged client-side at 500 or fewer rows.
- **Rationale**: Training scale (<= 500 documents per view) makes `LIKE` scans fast enough for the 2 s goal; avoids SQL Server full-text setup, which is not available in all LocalDB installs.
- **Alternatives considered**: SQL Server Full-Text Search (setup burden, not guaranteed in LocalDB); a separate search index (over-engineering).

## R12. Database evolution

- **Decision**: Keep `Database.EnsureCreated()` (current behavior). New tables appear only on a fresh database, so developers with an existing `ContosoDashboard` LocalDB must drop it once (`dotnet ef database drop --force` or `sqllocaldb stop mssqllocaldb` / `sqllocaldb delete mssqllocaldb`). The quickstart documents this.
- **Rationale**: Matches current app and the stakeholder "clean state" note; introducing migrations is out of scope and would change how existing students set up the project.
- **Alternatives considered**: add EF migrations (larger change to the starter project); runtime `CREATE TABLE` scripts (fragile).

## R13. Notifications on project upload

- **Decision**: When a document with a `ProjectId` is uploaded, create `DocumentAdded` notifications for the project manager and all members except the uploader. Best effort: notification failures are logged but do not fail the upload.
- **Rationale**: Spec FR-035 / story 8 scenario 3.
- **Alternatives considered**: notify including the uploader (noise).

## R14. Login claims

- **Decision**: Add a `Department` claim to the login claims alongside the existing NameIdentifier, Name, Email, Role (stakeholder requirement). Authorization decisions still read the database (R1), so the claim is for display and future use only.
- **Rationale**: Satisfies the stakeholder note without making security depend on it.
