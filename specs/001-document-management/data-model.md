# Data Model: Document Upload and Management

New entities follow existing conventions: `int` identity keys, data annotations on the entity, relationships and indexes in `ApplicationDbContext.OnModelCreating`, UTC `DateTime` values.

## Document

Table `Documents`. A stored file and its metadata.

| Field | Type | Rules |
|---|---|---|
| `DocumentId` | `int` PK, identity | Integer (not GUID), consistent with User/Project keys |
| `Title` | `string` (max 255) | Required |
| `Description` | `string?` (max 2000) | Optional |
| `Category` | `string` (max 50) | Required; text, one of the `DocumentCategories` constants: `Project Documents`, `Team Resources`, `Personal Files`, `Reports`, `Presentations`, `Other` |
| `Tags` | `string?` (max 500) | Optional; comma-separated, trimmed, case-insensitive duplicates removed |
| `OriginalFileName` | `string` (max 255) | Required; display and download name only; never used to build a path |
| `FilePath` | `string` (max 500) | Required, non-empty; relative storage path `{userId}/{projectId or "personal"}/{guid}.{ext}`; unique index |
| `FileType` | `string` (max 255) | Required; server-derived MIME type (for example `application/vnd.openxmlformats-officedocument.wordprocessingml.document`) |
| `FileSize` | `long` | Required; bytes, 1 to 26,214,400 |
| `UploadedByUserId` | `int` FK -> `Users` | Required; restrict delete |
| `ProjectId` | `int?` FK -> `Projects` | Optional; restrict delete |
| `TaskId` | `int?` FK -> `Tasks` | Optional; set null on delete |
| `UploadDate` | `DateTime` | Required; set on create |
| `UpdatedDate` | `DateTime?` | Set on metadata edit or file replace |

**Indexes**: `UploadedByUserId`; `ProjectId`; `TaskId`; `UploadDate`; `Category`; unique on `FilePath`.

**Relationships**: Document -> `UploadedBy` (`User`), `Project?`, `Task?`, many `DocumentShare`, many `DocumentActivity` (nullable link).

**Validation rules** (enforced in `DocumentService`, surfaced as messages):
- Title required, trimmed, 1-255 characters.
- Category must equal one of the six constants.
- Extension must be in the whitelist and content signature must match (see research R4).
- Size must be greater than 0 and at most 25 MB.
- `ProjectId`, when set, must be a project the uploader manages, belongs to, or (for Administrators) any project.
- When `TaskId` is set, `ProjectId` is forced to the task's `ProjectId` if the task has one.
- Tags: at most 10 tags, each at most 50 characters.

**State transitions**: Created (upload) -> Updated (metadata edit, file replace; `UpdatedDate` set) -> Deleted (rows removed; file removed). Shares are created/removed independently. There is no soft-delete state (out of scope).

## DocumentShare

Table `DocumentShares`. Grants view/preview/download to a user or a department.

| Field | Type | Rules |
|---|---|---|
| `DocumentShareId` | `int` PK, identity | |
| `DocumentId` | `int` FK -> `Documents` | Required; cascade delete |
| `SharedWithUserId` | `int?` FK -> `Users` | Exactly one of this or `SharedWithDepartment` is set; restrict delete |
| `SharedWithDepartment` | `string?` (max 100) | Matches `User.Department` exactly (case-insensitive) |
| `SharedByUserId` | `int` FK -> `Users` | Required; must equal the document's `UploadedByUserId`; restrict delete |
| `SharedDate` | `DateTime` | Required |

**Indexes**: `DocumentId`; `SharedWithUserId`; `SharedWithDepartment`; unique `(DocumentId, SharedWithUserId)` and `(DocumentId, SharedWithDepartment)` (filtered for non-null) so a target is shared at most once.

**Rules**: A user cannot share with themselves; sharing with a department the owner is not in is allowed; revoking deletes the row and removes access immediately.

## DocumentActivity

Table `DocumentActivities`. Immutable audit record.

| Field | Type | Rules |
|---|---|---|
| `DocumentActivityId` | `int` PK, identity | |
| `DocumentId` | `int?` FK -> `Documents` | Set null when the document is deleted |
| `DocumentTitle` | `string` (max 255) | Snapshot at the time of the action |
| `FileType` | `string` (max 255) | Snapshot, used by the "most uploaded types" report |
| `UserId` | `int` FK -> `Users` | The actor; restrict delete |
| `ActivityType` | `DocumentActivityType` enum | `Upload`, `Download`, `Preview`, `EditMetadata`, `ReplaceFile`, `Delete`, `Share`, `RevokeShare` |
| `Details` | `string?` (max 500) | For example the share target or the old/new file name |
| `ActivityDate` | `DateTime` | Required; UTC |

**Indexes**: `(ActivityType, ActivityDate)`; `UserId`; `DocumentId`.

## Changes to existing entities

- `Notification` / `NotificationType`: append `DocumentShared` and `DocumentAdded` at the end of the enum (existing integer values unchanged).
- `TaskItem`: add `Documents` navigation collection (`ICollection<Document>`).
- `User`: add `UploadedDocuments` navigation collection.
- `Project`: add `Documents` navigation collection.
- `DashboardSummary` (service DTO, not an entity): add `DocumentCount`.
- No existing columns change.

## View models (service layer, not persisted)

- `DocumentListItem`: `DocumentId`, `Title`, `Category`, `UploadDate`, `FileSize`, `FileType`, `ProjectId`, `ProjectName`, `UploadedByName`, `Tags`, `CanEdit`, `CanDelete`, `CanShare`, `CanReplace` (per-document permissions for the acting user).
- `DocumentQuery`: `Scope` (`Mine`, `SharedWithMe`, `Accessible`), `SearchText`, `Category`, `ProjectId`, `FromDate`, `ToDate`, `SortBy`, `SortDescending`.
- `DocumentUploadRequest`: `Title`, `Description`, `Category`, `Tags`, `ProjectId`, `TaskId`, `OriginalFileName`, `ContentLength`, `Content` (stream).
- `DocumentOperationResult`: `Succeeded`, `Error` (user-facing message), `Document` (when applicable).
- Report DTOs: `UploadTypeReportRow`, `TopUploaderReportRow`, `AccessPatternReportRow`.

## Seed data

None for documents. Categories are code constants and require no lookup rows. Seeded documents would reference files that do not exist on a fresh checkout. Existing seeded users (admin, project manager, team lead, employee in Engineering/IT) and the sample project are sufficient to exercise every role in [quickstart.md](quickstart.md).
