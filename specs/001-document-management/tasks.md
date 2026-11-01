# Tasks: Document Upload and Management

**Input**: Design documents from `/specs/001-document-management/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ (service-interfaces.md, http-endpoints.md, ui-pages.md), quickstart.md

**Tests**: Included. plan.md adds an xUnit project (`ContosoDashboard.Tests`) because the role/ownership access matrix is the highest-risk part of the feature. Tests cover services and infrastructure; pages and endpoints are verified with the manual scenarios in quickstart.md.

**Organization**: Tasks are grouped by user story so each story can be implemented and verified independently after the Foundational phase.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: User story label (US1..US9), used only in user-story phases
- Paths are relative to the repository root. The web project is `ContosoDashboard/`; the new test project is `ContosoDashboard.Tests/`.

## Path Conventions

- Web project (Blazor Server): `ContosoDashboard/Models`, `ContosoDashboard/Data`, `ContosoDashboard/Services`, `ContosoDashboard/Endpoints`, `ContosoDashboard/Pages`, `ContosoDashboard/Shared`
- Tests: `ContosoDashboard.Tests/`
- `IDocumentService` and `DocumentService` live in one file, `ContosoDashboard/Services/DocumentService.cs`. Each story adds its own methods to the interface and the class there, so tasks that edit that file run sequentially (no [P]).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Configuration, ignore rules, and the test project

- [ ] T001 Add a `FileStorage` section with `"RootPath": "AppData/uploads"` to `ContosoDashboard/appsettings.json`
- [ ] T002 [P] Add `ContosoDashboard/AppData/uploads/` to `.gitignore` so uploaded files are never committed
- [ ] T003 Create `ContosoDashboard.Tests/ContosoDashboard.Tests.csproj` targeting `net8.0` with `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`, `Microsoft.EntityFrameworkCore.InMemory` 8.0.0 (test-only) and a `ProjectReference` to `..\ContosoDashboard\ContosoDashboard.csproj`; confirm `dotnet test ContosoDashboard.Tests` runs (zero tests)
- [ ] T004 [P] Create `ContosoDashboard.Tests/TestSupport/TestDb.cs`: factory returning an `ApplicationDbContext` on a uniquely named InMemory database, with the four seeded users (admin in IT; Camille project manager, Floris team lead, Ni Kang employee in Engineering), project 1 with members Floris and Ni, plus extra users added by the helper: an Employee in Engineering who is NOT a project member, an Employee in another department, and a second Team Lead in another department
- [ ] T005 [P] Create `ContosoDashboard.Tests/TestSupport/Fakes.cs`: `FakeFileStorageService` (in-memory dictionary, with switches to throw on upload/delete and to record calls) and `FakeMalwareScanService` (configurable result: Clean, Infected, Unavailable)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Entities, database mapping, infrastructure abstractions, validation, and the authorization core that every story depends on

**CRITICAL**: No user story work can begin until this phase is complete

- [ ] T006 [P] Create `ContosoDashboard/Models/Document.cs` with entity `Document` and static class `DocumentCategories` (`Project Documents`, `Team Resources`, `Personal Files`, `Reports`, `Presentations`, `Other`; plus `All` list). Fields and constraints per data-model.md: `DocumentId` int PK identity; `Title` [Required][MaxLength(255)]; `Description` string? [MaxLength(2000)]; `Category` [Required][MaxLength(50)] text (not enum); `Tags` string? [MaxLength(500)]; `OriginalFileName` [Required][MaxLength(255)]; `FilePath` [Required][MaxLength(500)] relative path, never empty; `FileType` [Required][MaxLength(255)]; `FileSize` long; `UploadedByUserId` int; `ProjectId` int?; `TaskId` int?; `UploadDate` DateTime UTC; `UpdatedDate` DateTime?; navigation properties `UploadedBy`, `Project`, `Task`, `Shares`, `Activities`
- [ ] T007 [P] Create `ContosoDashboard/Models/DocumentShare.cs`: `DocumentShareId` int PK; `DocumentId` int; `SharedWithUserId` int?; `SharedWithDepartment` string? [MaxLength(100)] (exactly one of user/department is set); `SharedByUserId` int; `SharedDate` DateTime; navigation properties
- [ ] T008 [P] Create `ContosoDashboard/Models/DocumentActivity.cs`: entity (`DocumentActivityId`, `DocumentId` int?, `DocumentTitle` [MaxLength(255)], `FileType` [MaxLength(255)], `UserId`, `ActivityType`, `Details` string? [MaxLength(500)], `ActivityDate` UTC) and enum `DocumentActivityType` { Upload, Download, Preview, EditMetadata, ReplaceFile, Delete, Share, RevokeShare }
- [ ] T009 [P] Append `DocumentShared` and `DocumentAdded` to the end of `NotificationType` in `ContosoDashboard/Models/Notification.cs` (do not renumber existing values)
- [ ] T010 [P] Add navigation collections: `Documents` in `ContosoDashboard/Models/TaskItem.cs`, `UploadedDocuments` in `ContosoDashboard/Models/User.cs`, `Documents` in `ContosoDashboard/Models/Project.cs`
- [ ] T011 Update `ContosoDashboard/Data/ApplicationDbContext.cs` (after T006-T010): add `DbSet<Document> Documents`, `DbSet<DocumentShare> DocumentShares`, `DbSet<DocumentActivity> DocumentActivities`; relationships per data-model.md (Document -> UploadedBy restrict, Project restrict, Task set null; DocumentShare -> Document cascade, users restrict; DocumentActivity -> Document set null, User restrict); indexes: Document on `UploadedByUserId`, `ProjectId`, `TaskId`, `UploadDate`, `Category`, unique `FilePath`; DocumentShare on `DocumentId`, `SharedWithUserId`, `SharedWithDepartment`, unique filtered `(DocumentId, SharedWithUserId)` and `(DocumentId, SharedWithDepartment)`; DocumentActivity on `(ActivityType, ActivityDate)`, `UserId`, `DocumentId`. Do not add seed rows for documents
- [ ] T012 [P] Create `ContosoDashboard/Services/IFileStorageService.cs` with the `IFileStorageService` contract from contracts/service-interfaces.md (`UploadAsync`, `DeleteAsync`, `DownloadAsync`, `GetUrlAsync`)
- [ ] T013 [P] Create `ContosoDashboard/Services/IMalwareScanService.cs` with `MalwareScanStatus`, `MalwareScanResult`, `IMalwareScanService`
- [ ] T014 [P] Create `ContosoDashboard/Services/DocumentModels.cs` with `DocumentListItem` (including `CanEdit`, `CanDelete`, `CanShare`, `CanReplace`), `DocumentQuery` (Scope enum `Mine`/`SharedWithMe`/`Accessible`, SearchText, Category, ProjectId, FromDate, ToDate, SortBy, SortDescending), `DocumentUploadRequest`, `DocumentOperationResult` (static `Ok`/`Fail(message)` helpers), `DocumentContent`, `DocumentShareInfo`, and report row types `UploadTypeReportRow`, `TopUploaderReportRow`, `AccessPatternReportRow`
- [ ] T015 [P] Create `ContosoDashboard/Services/DocumentFileValidator.cs` (static): extension whitelist `.pdf .doc .docx .xls .xlsx .ppt .pptx .txt .jpg .jpeg .png`; extension-to-MIME map (server-derived, max 255 chars); `MaxFileSizeBytes = 25 * 1024 * 1024`; checks for empty file and over-size; magic-byte check per research R4 (`%PDF`, `PK\x03\x04`, OLE `D0 CF 11 E0 A1 B1 1A E1`, JPEG `FF D8 FF`, PNG `89 50 4E 47 0D 0A 1A 0A`, `.txt` has no NUL bytes); `IsPreviewable(fileType)` true only for `application/pdf`, `image/jpeg`, `image/png`; user-facing error messages that state the size limit and supported types
- [ ] T016 Implement `ContosoDashboard/Services/LocalFileStorageService.cs` (after T012): root from `FileStorage:RootPath` resolved to an absolute path against the content root; reject any path resolving outside the root (`ArgumentException`); `UploadAsync` fails if the file exists; `DeleteAsync` ignores a missing file; `DownloadAsync` returns a read stream or throws `FileNotFoundException`; `GetUrlAsync` returns the storage path (not browsable)
- [ ] T017 [P] Implement `ContosoDashboard/Services/LocalSignatureScanService.cs` (after T013): report `Infected` when the stream contains the EICAR test string; wrap all work in try/catch and a timeout so any failure is returned as `Unavailable`; rewind the stream and never dispose it
- [ ] T018 Create `ContosoDashboard/Services/DocumentService.cs` with an initially empty `IDocumentService` interface and class `DocumentService` (constructor takes `ApplicationDbContext`, `IFileStorageService`, `IMalwareScanService`, `INotificationService`, `ILogger<DocumentService>`); add private helpers `LoadUserAsync(userId)` (role and department from the database, null if unknown) and `LogActivityAsync(documentId, title, fileType, userId, type, details)` that adds a `DocumentActivity` to the context without saving (callers save in the same `SaveChangesAsync`)
- [ ] T019 In `ContosoDashboard/Services/DocumentService.cs` add `ApplyViewFilter(IQueryable<Document>, User)` and a per-document permissions function implementing the matrix in research.md R1: view = uploader, Administrator, project manager or member of the document's project, Team Lead in the same non-null department as the uploader excluding `Personal Files`, or a share to the user id or the user's department; edit metadata and delete = uploader, Administrator, manager of the document's project; replace and share/revoke = uploader only
- [ ] T020 Update `ContosoDashboard/Program.cs`: register `IFileStorageService` -> `LocalFileStorageService`, `IMalwareScanService` -> `LocalSignatureScanService`, `IDocumentService` -> `DocumentService` (scoped); at startup resolve the storage root, create the directory if missing, and log an error and refuse to start the feature if the root is inside `wwwroot`
- [ ] T021 [P] Create `ContosoDashboard.Tests/LocalFileStorageServiceTests.cs`: upload/download/delete round trip, deleting a missing file is a no-op, uploading to an existing path fails, `..` and rooted paths are rejected
- [ ] T022 [P] Create `ContosoDashboard.Tests/LocalSignatureScanServiceTests.cs`: clean content -> `Clean`, EICAR string -> `Infected`, a stream that throws -> `Unavailable`, stream not disposed and position restored
- [ ] T023 [P] Create `ContosoDashboard.Tests/DocumentFileValidatorTests.cs`: every whitelisted extension accepted with matching signature; `.exe` rejected; renamed file with wrong signature rejected; zero-byte rejected; 25 MB accepted and 25 MB + 1 byte rejected; MIME type derived from the extension; preview allowed only for PDF/JPEG/PNG

**Checkpoint**: `dotnet build` passes, foundation tests pass; user story phases can begin

---

## Phase 3: User Story 1 - Upload a Document with Metadata (Priority: P1) MVP

**Goal**: A signed-in user uploads one or more valid files with metadata, sees progress and a success or error message; invalid files are rejected and nothing is stored.

**Independent Test**: Log in as Ni Kang, upload a valid PDF with title and category and see a success message; upload an oversize file, an `.exe`, an empty file, a renamed file, and the EICAR file and see rejections with no new rows or files (quickstart Scenario 1).

### Tests for User Story 1

- [ ] T024 [P] [US1] Create `ContosoDashboard.Tests/DocumentServiceUploadTests.cs`: valid upload stores file at `{userId}/personal/{guid}.pdf` and creates a record with correct size, MIME type, uploader, date; reject over 25 MB, empty, unsupported extension, signature mismatch; reject missing/blank title and invalid category; reject `Infected` and `Unavailable` scans (message says "try again later" for unavailable) with no record and no stored file; storage failure creates no record; DB failure after file save deletes the saved file; project association rejected for a non-member and allowed for a member, manager, and Administrator; user-supplied filename never appears in the stored path

### Implementation for User Story 1

- [ ] T025 [US1] Add `UploadAsync(DocumentUploadRequest, int requestingUserId)` to `IDocumentService` and implement it in `ContosoDashboard/Services/DocumentService.cs`: sequence = validate (T015) -> authorize project (manager, member, or Administrator) -> scan (reject Infected/Unavailable, fail closed) -> generate path `{userId}/{projectId or "personal"}/{guid}.{ext}` -> save file -> save `Document` plus `Upload` activity in one `SaveChangesAsync`; on database failure delete the saved file; trim title, normalize tags (max 10 tags, each max 50 chars, case-insensitive de-duplication)
- [ ] T026 [US1] Create `ContosoDashboard/Shared/DocumentUploadDialog.razor`: modal with `InputFile Multiple` (rotate `@key` after a batch), batch-level Category (required, six values), Project (limited to the user's managed/member projects via `IProjectService.GetUserProjectsAsync`), Tags; per-file Title (defaults to filename without extension) and Description; capture name/size/content type before opening the stream, copy to `MemoryStream` in chunks with a per-file progress percentage, set the `IBrowserFile` reference to null, call `IDocumentService.UploadAsync` per file sequentially; per-file status (saved, or the service error message); client-side checks for extension, 25 MB, non-empty; accept optional `ProjectId` and `TaskId` parameters and an `OnUploaded` callback; no `DbContext` use
- [ ] T027 [US1] Create `ContosoDashboard/Pages/Documents.razor` at `/documents` with `[Authorize]`, a page title, an "Upload" button opening `DocumentUploadDialog`, and a success/failure alert area; read the user id from the `NameIdentifier` claim like the other pages
- [ ] T028 [P] [US1] Add a **Documents** link (`/documents`, Bootstrap Icons `bi-file-earmark-text`) to `ContosoDashboard/Shared/NavMenu.razor`
- [ ] T029 [P] [US1] Add the `Department` claim (when the user has a department) to the claims list in `ContosoDashboard/Pages/Login.cshtml.cs` alongside NameIdentifier, Name, Email, Role (display only; authorization still reads the database)

**Checkpoint**: Upload works end to end and is independently verifiable (MVP)

---

## Phase 4: User Story 2 - Browse, Sort, and Filter My Documents (Priority: P1)

**Goal**: Users see their uploaded documents and can sort by title, upload date, category, file size and filter by category, project, and date range.

**Independent Test**: Upload documents with differing categories, projects, and dates; verify the list, every sort, every filter, combined filters, and the empty state (quickstart Scenario 2).

### Tests for User Story 2

- [ ] T030 [P] [US2] Create `ContosoDashboard.Tests/DocumentServiceBrowseTests.cs`: `Mine` scope returns only the caller's uploads; each sort field ascending and descending; category, project, and date-range filters alone and combined; empty result; list items carry title, category, upload date, size, project name

### Implementation for User Story 2

- [ ] T031 [US2] Add `GetDocumentsAsync(DocumentQuery, int requestingUserId)` to `IDocumentService` and implement the `Mine` scope in `ContosoDashboard/Services/DocumentService.cs`: filter by `UploadedByUserId`, apply category/project/date filters and sorting, project to `DocumentListItem` without loading file content, populate permission flags from T019
- [ ] T032 [P] [US2] Create `ContosoDashboard/Shared/DocumentList.razor`: parameters `Items`, `ShowUploader`, `ShowProject`, callbacks `OnEdit`, `OnDelete`, `OnShare`, `OnReplace`; columns Title, Category, Upload date, Size (human readable), Project (plus Uploader); empty-state alert; action buttons shown only when the matching permission flag is true
- [ ] T033 [US2] Update `ContosoDashboard/Pages/Documents.razor`: add the **My Documents** tab with sort selector (title, upload date, category, file size, direction), filters (category, project, from/to dates), refresh after upload, and `DocumentList`

**Checkpoint**: US1 and US2 together deliver a usable upload-and-browse loop

---

## Phase 5: User Story 3 - Download and Preview Documents (Priority: P1)

**Goal**: Users download any document they can access and preview PDFs and images in the browser; inaccessible documents return 404.

**Independent Test**: Upload a PDF and a PNG, download both (byte-identical), preview both, and confirm another user without access gets 404 for both URLs (quickstart Scenario 3).

### Tests for User Story 3

- [ ] T034 [P] [US3] Create `ContosoDashboard.Tests/DocumentServiceAccessTests.cs`: `OpenForDownloadAsync` returns the stored bytes, original filename, and MIME type for the uploader; returns null for a user without view access and for a nonexistent id; `OpenForPreviewAsync` returns content for PDF/PNG/JPEG and null for `.docx`/`.txt`; Download and Preview activity rows are recorded only on success

### Implementation for User Story 3

- [ ] T035 [US3] Add `GetDocumentAsync`, `OpenForDownloadAsync`, and `OpenForPreviewAsync` to `IDocumentService` and implement them in `ContosoDashboard/Services/DocumentService.cs`: load the user, apply `ApplyViewFilter`, return null when not found or not viewable, open the stream via `IFileStorageService.DownloadAsync`, log `Download`/`Preview` activity and save, preview returns null when the type is not previewable
- [ ] T036 [P] [US3] Create `ContosoDashboard/Endpoints/DocumentEndpoints.cs` with `MapDocumentEndpoints(this IEndpointRouteBuilder)` implementing `GET /api/documents/{id:int}/download` and `GET /api/documents/{id:int}/preview` per contracts/http-endpoints.md: `RequireAuthorization()`, user id from `NameIdentifier` (401 if missing), 404 for null results, 400 for non-previewable preview, attachment vs inline disposition, `Cache-Control: private, no-store`, generic 500 when the stored file is missing (log details)
- [ ] T037 [US3] Call `app.MapDocumentEndpoints()` in `ContosoDashboard/Program.cs` after `UseAuthorization()` and before `MapFallbackToPage`
- [ ] T038 [US3] Update `ContosoDashboard/Shared/DocumentList.razor`: always show a **Download** link to `/api/documents/{id}/download`; show a **Preview** link (`target="_blank"`, `rel="noopener"`) only for PDF/JPEG/PNG rows

**Checkpoint**: P1 stories complete; the core "store, find, retrieve" value is delivered

---

## Phase 6: User Story 4 - Project Documents and Role-Based Access (Priority: P2)

**Goal**: Project members see and download all documents of their project; roles get the access defined in the permission matrix; Team Leads see team members' non-personal documents; Administrators access everything.

**Independent Test**: As Camille upload to the sample project; Floris and Ni (members) see and download it; the non-member cannot, by list, search, or URL; Floris cannot see Ni's Personal Files; admin can access all (quickstart Scenario 4).

### Tests for User Story 4

- [ ] T039 [P] [US4] Create `ContosoDashboard.Tests/DocumentServiceAuthorizationTests.cs`: table-driven matrix over {uploader, Administrator, manager of the project, member, same-department Team Lead, other-department Team Lead, non-member employee, share recipient} x {view, download, edit metadata, replace, delete, share} for (a) a personal document, (b) a project document, (c) a "Personal Files" document with a project, (d) a "Reports" document without a project; assert that no-access users get null/empty and nonexistent ids behave identically

### Implementation for User Story 4

- [ ] T040 [US4] Add `GetProjectDocumentsAsync(int projectId, int requestingUserId)` to `IDocumentService` and implement it in `ContosoDashboard/Services/DocumentService.cs`: returns documents of the project only if the user manages it, is a member, or is an Administrator; otherwise an empty list
- [ ] T041 [US4] Update `ContosoDashboard/Pages/ProjectDetails.razor`: inject `IDocumentService`, add a **Documents** section listing project documents via `DocumentList` (`ShowUploader` true), an Upload button (project preselected) for the project manager and members using `DocumentUploadDialog`, and refresh after upload
- [ ] T042 [US4] Review `ContosoDashboard/Services/DocumentService.cs` `ApplyViewFilter` and permissions against the matrix tests; fix any gap found by T039 (including Team Lead excluded from `Personal Files` and Project Manager rights limited to projects they manage)

**Checkpoint**: Role-based access verified by automated tests

---

## Phase 7: User Story 5 - Search Documents (Priority: P2)

**Goal**: Users search title, description, tags, uploader name, and project name across documents they may view, in under 2 seconds.

**Independent Test**: Search each field; results include only accessible documents; Team Lead finds team members' non-personal documents; about 500 documents return within 2 seconds (quickstart Scenario 5).

### Tests for User Story 5

- [ ] T043 [P] [US5] Create `ContosoDashboard.Tests/DocumentServiceSearchTests.cs`: matches on each of the five fields (case-insensitive); results never include documents the caller cannot view; search combines with category/project/date filters; blank search returns all accessible documents; 500-document dataset returns in under 2 seconds

### Implementation for User Story 5

- [ ] T044 [US5] Extend `GetDocumentsAsync` in `ContosoDashboard/Services/DocumentService.cs` with the `Accessible` scope: start from `ApplyViewFilter`, apply `SearchText` as `Contains` over title, description, tags, uploader display name, and project name, reuse the filters and sorting from US2
- [ ] T045 [US5] Update `ContosoDashboard/Pages/Documents.razor`: add the **Search** tab with a text box, optional filters, results in `DocumentList` (`ShowUploader` true, `ShowProject` true), and an empty-state message

**Checkpoint**: Search complete and access-safe

---

## Phase 8: User Story 6 - Edit Metadata, Replace File, and Delete (Priority: P2)

**Goal**: Uploaders (and the permitted roles) edit metadata; uploaders replace the file; delete is permanent after confirmation.

**Independent Test**: Edit, replace, and delete a document and confirm persistence, new file served, old file gone, and denial for unauthorized users (quickstart Scenario 6).

### Tests for User Story 6

- [ ] T046 [P] [US6] Create `ContosoDashboard.Tests/DocumentServiceManageTests.cs`: metadata edit by uploader, Administrator, and project manager succeeds and by others fails; invalid title/category rejected; replace by uploader only, with size/type updated, new file stored, old file deleted, original kept when the new file is invalid or the scan fails; delete by uploader, Administrator, and project manager removes the record, shares, and file while keeping activity rows with null `DocumentId`; delete by others fails; file-delete failure after DB delete still succeeds

### Implementation for User Story 6

- [ ] T047 [US6] Add `UpdateMetadataAsync` to `IDocumentService` and implement it in `ContosoDashboard/Services/DocumentService.cs`: permission check, same title/category/tags validation as upload, set `UpdatedDate`, log `EditMetadata`
- [ ] T048 [US6] Add `ReplaceFileAsync` to `IDocumentService` and implement it in `ContosoDashboard/Services/DocumentService.cs`: uploader only; same validation and scan as upload; save the new file at a new GUID path, update `FilePath`, `FileType`, `FileSize`, `OriginalFileName`, `UpdatedDate`, log `ReplaceFile` with the old and new filenames, then delete the old file; on any failure before the record update delete the new file and keep the original
- [ ] T049 [US6] Add `DeleteAsync` to `IDocumentService` and implement it in `ContosoDashboard/Services/DocumentService.cs`: permission check, remove document and shares in one save after logging `Delete` with title and file type snapshots, then delete the file and log (not throw) a failure
- [ ] T050 [P] [US6] Create `ContosoDashboard/Shared/DocumentEditDialog.razor`: edit title, description, category, tags; for users with `CanReplace`, a single-file `InputFile` (same MemoryStream pattern as the upload dialog) to replace the file; show service errors
- [ ] T051 [US6] Update `ContosoDashboard/Pages/Documents.razor` and `ContosoDashboard/Pages/ProjectDetails.razor`: wire `OnEdit` to `DocumentEditDialog` and `OnDelete` to a confirmation modal that states the deletion is permanent, with Cancel doing nothing; refresh lists afterwards

**Checkpoint**: Full document lifecycle works

---

## Phase 9: User Story 7 - Share Documents and "Shared with Me" (Priority: P3)

**Goal**: Owners share with users or departments and revoke; recipients are notified and see the document in Shared with Me with view/preview/download only.

**Independent Test**: Ni shares with Floris; Floris is notified, sees it under Shared with Me, can download but not edit/delete/re-share; Ni revokes and access ends (quickstart Scenario 7).

### Tests for User Story 7

- [ ] T052 [P] [US7] Create `ContosoDashboard.Tests/DocumentServiceSharingTests.cs`: owner can share with a user and with a department; non-owner (including Administrator and project manager) cannot share or revoke; duplicate share, self-share, unknown user, and blank department rejected; recipients (user and department members) gain view/download but no edit/delete/replace/share; notifications created for the user or for each department member except the sharer; revoke removes access immediately; `SharedWithMe` lists shared documents excluding the caller's own; Share and RevokeShare activity logged

### Implementation for User Story 7

- [ ] T053 [US7] Add `ShareWithUserAsync`, `ShareWithDepartmentAsync`, `RevokeShareAsync`, and `GetSharesAsync` to `IDocumentService` and implement them in `ContosoDashboard/Services/DocumentService.cs`: uploader-only, one target per share row (per data-model.md), log `Share`/`RevokeShare` with the target in `Details`, create `NotificationType.DocumentShared` notifications through `INotificationService` (best effort, failures logged)
- [ ] T054 [US7] Extend `GetDocumentsAsync` in `ContosoDashboard/Services/DocumentService.cs` with the `SharedWithMe` scope: documents with a share to the user id or the user's department, excluding the user's own uploads
- [ ] T055 [P] [US7] Create `ContosoDashboard/Shared/DocumentShareDialog.razor`: choose a user (via `IUserService.GetAllUsersAsync`, excluding self) or a department, list current shares with Revoke buttons, show service errors
- [ ] T056 [US7] Update `ContosoDashboard/Pages/Documents.razor`: add the **Shared with Me** tab (`DocumentList`, `ShowUploader` true) and wire `OnShare` to `DocumentShareDialog`

**Checkpoint**: Sharing complete

---

## Phase 10: User Story 8 - Task and Dashboard Integration (Priority: P3)

**Goal**: Documents are visible and attachable from a task detail page, the dashboard shows a document count and Recent Documents, and project members are notified of new project documents.

**Independent Test**: Upload from a task and attach an existing document (project association follows the task); the dashboard shows the count and last 5 uploads; project members get a notification (quickstart Scenario 8).

### Tests for User Story 8

- [ ] T057 [P] [US8] Create `ContosoDashboard.Tests/DocumentServiceIntegrationTests.cs`: attaching sets `TaskId` and aligns `ProjectId` to the task's project; only the uploader who can view the task may attach; detach clears `TaskId`; `GetTaskDocumentsAsync` is access-filtered; `GetRecentDocumentsAsync` returns the caller's last 5 uploads newest first; `GetDocumentCountAsync` counts the caller's uploads; uploading to a project creates `DocumentAdded` notifications for the manager and all members except the uploader and a failed notification does not fail the upload

### Implementation for User Story 8

- [ ] T058 [US8] Add `AttachToTaskAsync`, `DetachFromTaskAsync`, and `GetTaskDocumentsAsync` to `IDocumentService` and implement them in `ContosoDashboard/Services/DocumentService.cs` (task visibility checked through `ITaskService.GetTaskByIdAsync`; inject it or load the task with the same rule); also let `UploadAsync` accept `TaskId`, validating the task and setting `ProjectId` from it
- [ ] T059 [US8] Add `GetRecentDocumentsAsync(requestingUserId, count = 5)` and `GetDocumentCountAsync(requestingUserId)` to `IDocumentService` and implement them in `ContosoDashboard/Services/DocumentService.cs`
- [ ] T060 [US8] In `UploadAsync` in `ContosoDashboard/Services/DocumentService.cs`, after a successful save of a project document create `NotificationType.DocumentAdded` notifications for the project manager and members except the uploader; catch and log notification failures
- [ ] T061 [P] [US8] Add `DocumentCount` to `DashboardSummary` and populate it in `ContosoDashboard/Services/DashboardService.cs` via `IDocumentService.GetDocumentCountAsync` (inject `IDocumentService`)
- [ ] T062 [US8] Update `ContosoDashboard/Pages/Index.razor`: add a **Documents** summary card showing `summary.DocumentCount` and a **Recent Documents** widget (last 5 uploads, title linking to download, category, date, with a link to `/documents`)
- [ ] T063 [US8] Create `ContosoDashboard/Pages/TaskDetails.razor` at `/tasks/{taskId:int}` with `[Authorize]`: load the task through `ITaskService.GetTaskByIdAsync` (not found/denied message), show title, status, priority, due date, project, and a **Documents** section using `DocumentList`, an Upload button (`TaskId` preset) and an **Attach existing** picker listing the user's own unattached uploads
- [ ] T064 [US8] Update `ViewTaskDetails` in `ContosoDashboard/Pages/Tasks.razor` to navigate to `/tasks/{taskId}` (inject `NavigationManager`)

**Checkpoint**: Integrations complete

---

## Phase 11: User Story 9 - Activity Logging and Admin Reports (Priority: P3)

**Goal**: Every document action is recorded and Administrators can view the three reports.

**Independent Test**: Perform each action, then as admin confirm entries and reports; a non-admin gets nothing (quickstart Scenario 9).

### Tests for User Story 9

- [ ] T065 [P] [US9] Create `ContosoDashboard.Tests/DocumentActivityAndReportTests.cs`: each of upload, download, preview, edit metadata, replace, delete, share, and revoke writes exactly one activity row with the actor and a title/file-type snapshot (and Administrator actions are included); deleted documents keep their activity rows; the three reports return correct aggregates and date filtering for Administrators and empty lists for any other role

### Implementation for User Story 9

- [ ] T066 [US9] Create `ContosoDashboard/Services/DocumentReportService.cs` with `IDocumentReportService` and `DocumentReportService` per contracts/service-interfaces.md: `GetUploadsByTypeAsync` (Upload activities grouped by `FileType`), `GetTopUploadersAsync` (Upload activities grouped by user, top N), `GetAccessPatternsAsync` (Download and Preview counts per document title and per day); every method returns an empty list unless the caller is an Administrator (role read from the database)
- [ ] T067 [US9] Register `IDocumentReportService` -> `DocumentReportService` (scoped) in `ContosoDashboard/Program.cs`
- [ ] T068 [US9] Create `ContosoDashboard/Pages/DocumentReports.razor` at `/documents/reports` with `[Authorize(Policy = "Administrator")]`: optional from/to date, three tables (types, top uploaders, access patterns), empty-state messages
- [ ] T069 [P] [US9] Add a **Document Reports** link (`/documents/reports`) to `ContosoDashboard/Shared/NavMenu.razor` wrapped in `AuthorizeView Roles="Administrator"`

**Checkpoint**: All user stories implemented

---

## Phase 12: Polish & Cross-Cutting Concerns

- [ ] T070 [P] Update `README.md`: add Document Management to the Features list, note the `FileStorage:RootPath` setting, the `AppData/uploads` location (outside `wwwroot`), the EICAR test file for malware scanning, and the one-time LocalDB reset required because the app uses `EnsureCreated()`
- [ ] T071 [P] Security review of the finished feature against Constitution III: confirm every new page has `[Authorize]`, no uploaded file is reachable under `wwwroot`, endpoints return 404 for inaccessible documents, no user-supplied filename is used in any storage path, and existing security headers in `ContosoDashboard/Program.cs` are unchanged
- [ ] T072 Run `dotnet build ContosoDashboard/ContosoDashboard.csproj` and `dotnet test ContosoDashboard.Tests/ContosoDashboard.Tests.csproj`; fix new warnings and failures
- [ ] T073 Reset LocalDB as described in quickstart.md and run quickstart Scenarios 1-10 manually with at least two different mock users, recording any deviation
- [ ] T074 Verify performance targets from quickstart.md (25 MB upload within 30 s, 500-document list and search within 2 s, preview within 3 s); add or adjust indexes in `ContosoDashboard/Data/ApplicationDbContext.cs` only if a target is missed
- [ ] T075 [P] Remove any leftover TODO comments introduced by this feature and verify `git status` shows no files under `ContosoDashboard/AppData/uploads`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies; T002, T004, T005 can run in parallel after T003 exists (T004/T005 compile only after T003)
- **Foundational (Phase 2)**: depends on Setup; blocks all user stories. Order: models T006-T010 -> T011 -> (T012-T015 in parallel) -> T016/T017 -> T018 -> T019 -> T020; tests T021-T023 after their targets
- **User Stories (Phases 3-11)**: each depends only on Foundational, plus the shared UI/service files noted below
- **Polish (Phase 12)**: after the stories you intend to ship

### User Story Dependencies

- **US1 (P1)**: after Foundational; no other story dependency
- **US2 (P1)**: after Foundational; shares `Documents.razor` with US1 (do after T027) and needs uploaded data to demo
- **US3 (P1)**: after Foundational; `DocumentList` links need T032 (US2)
- **US4 (P2)**: after Foundational and US1 (upload) and T032; the matrix tests (T039) also exercise US3 and US7 behavior, so run its share-recipient rows after US7 or mark those rows pending
- **US5 (P2)**: after US2 (extends `GetDocumentsAsync`)
- **US6 (P2)**: after US2 (list actions) and US1 (upload validation reuse)
- **US7 (P3)**: after US2 (`GetDocumentsAsync`) and US3 (access behavior)
- **US8 (P3)**: after US1 (upload) and US2 (list); T060 edits `UploadAsync` from US1
- **US9 (P3)**: logging calls are added by earlier stories; reports need at least US1 and US3 data; T065 audits all stories, so run it last

### Within Each User Story

- Tests are written first and should fail before the implementation task
- Entity and service tasks before pages; service methods in `DocumentService.cs` are sequential
- Story complete before moving to the next priority

### Parallel Opportunities

- Setup: T002, T004, T005
- Foundational: T006-T010; T012-T015; T017; T021-T023
- Across stories (with different people): after Foundational, US1's page work (T026-T029) can proceed alongside US3's endpoint (T036) because they touch different files; `DocumentService.cs` edits must stay sequential

---

## Parallel Example: Foundational Phase

```text
Launch together after T005:
  T006 Document.cs          T007 DocumentShare.cs     T008 DocumentActivity.cs
  T009 Notification enums   T010 Navigation collections

Launch together after T011:
  T012 IFileStorageService  T013 IMalwareScanService  T014 DocumentModels  T015 DocumentFileValidator
```

## Parallel Example: User Story 1

```text
Launch together:
  T024 DocumentServiceUploadTests (write first)
  T028 NavMenu Documents link
  T029 Department claim in Login.cshtml.cs
Then sequentially: T025 UploadAsync -> T026 DocumentUploadDialog -> T027 Documents.razor
```

---

## Implementation Strategy

### MVP First (User Stories 1-3, all P1)

1. Phase 1 Setup, Phase 2 Foundational (blocks everything)
2. US1 upload, then stop and validate with quickstart Scenario 1
3. US2 browse, US3 download/preview; validate Scenarios 2-3. This is the smallest release that delivers the "central, secure place for documents" value

### Incremental Delivery

1. P2: US4 (roles and project documents), US5 (search), US6 (edit/replace/delete)
2. P3: US7 (sharing), US8 (task and dashboard integration), US9 (audit and reports)
3. Polish (README, security review, full quickstart, performance)

Each increment keeps earlier stories working because the authorization core (T018-T019) and the single-file service rule are in place from the start.

### Notes

- Activity logging calls are added inside each story's service methods; US9 only adds reports and the audit test
- Stakeholder implementation details (path pattern, `IFileStorageService`, Blazor `MemoryStream` pattern, integer `DocumentId`, text category) are encoded in T006, T012, T016, T025, T026
- Commit after each task or logical group; stop at any checkpoint to validate a story independently
