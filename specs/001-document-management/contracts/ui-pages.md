# Contract: UI Pages and Components

All pages use `@attribute [Authorize]`, inject service interfaces only (no `DbContext`), and read the user id from the `NameIdentifier` claim, like the existing pages.

## Routes

| Route | Component | Roles | Purpose |
|---|---|---|---|
| `/documents` | `Pages/Documents.razor` | Any signed-in user | Tabs: **My Documents**, **Shared with Me**, **Search** (all documents the user may view, including team documents for Team Leads) |
| `/documents/reports` | `Pages/DocumentReports.razor` | `[Authorize(Policy = "Administrator")]` | Uploads by type, top uploaders, access patterns |
| `/tasks/{taskId:int}` | `Pages/TaskDetails.razor` | Any user who can view the task | Task summary plus Documents section (list, upload, attach existing) |
| `/projects/{projectId:int}` | `Pages/ProjectDetails.razor` (changed) | Existing rule | New Documents section listing project documents with upload for the project manager and members |
| `/` | `Pages/Index.razor` (changed) | Any signed-in user | New "Documents" summary card (count) and "Recent Documents" widget (last 5) |

Navigation: add **Documents** to `NavMenu.razor`; add **Document Reports** only when the user is an Administrator.

## Shared component: `Shared/DocumentList.razor`

Parameters: `Items` (`List<DocumentListItem>`), `ShowUploader`, `ShowProject`, callbacks `OnEdit`, `OnDelete`, `OnShare`, `OnReplace`.

Behavior:
- Columns: Title, Category, Upload date, Size, Project (plus Uploader when `ShowUploader`).
- Row actions shown only when the matching `CanEdit/CanDelete/CanShare/CanReplace` flag is true; Download always shown; Preview shown only for PDF/JPEG/PNG.
- Download and Preview are plain links to the HTTP endpoints (Preview with `target="_blank"` and `rel="noopener"`).
- Delete always opens a confirmation dialog before calling the service.
- Empty state message when `Items` is empty.

## Upload dialog behavior (in `Documents.razor`, reused from `TaskDetails.razor` and `ProjectDetails.razor`)

- `InputFile` with `Multiple`; rotate its `@key` after a completed batch.
- Batch-level fields: Category (required), Project (optional, list limited to projects the user manages or belongs to), Tags (optional). Per-file fields: Title (required, defaults to the filename without extension), Description (optional).
- Client-side checks mirror server rules (extension, 25 MB, non-empty) for fast feedback; the server rechecks everything.
- Per-file progress bar and status (queued, uploading with percent, saved, error with message). Valid files in a batch succeed independently of invalid ones.
- Primary action reachable in three clicks after choosing files: open dialog -> confirm metadata -> upload (SC-005).

## Document filters and sorting (My Documents tab)

- Sort: Title, Upload date, Category, File size, ascending/descending.
- Filter: Category (six values), Project (user's projects), Date range (upload date from/to). Filters combine with AND.
- Search tab: single text box searching title, description, tags, uploader name, project name.

## Accessibility and consistency

- Use existing Bootstrap 5.3 table, badge, modal, and alert styles from the other pages; icons from Bootstrap Icons.
- Status and errors are rendered in `alert` elements with text (not color alone).
