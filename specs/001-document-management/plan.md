# Implementation Plan: Document Upload and Management

**Branch**: `001-document-management` | **Date**: 2026-11-02 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/001-document-management/spec.md`

## Summary

Add centralized document upload, organization, search, sharing, task/dashboard integration, and
audit reporting to the existing ContosoDashboard Blazor Server application. Documents are stored
on the local filesystem outside `wwwroot` behind an `IFileStorageService` abstraction, malware
scanning is behind an `IMalwareScanService` abstraction (fail-closed), and all access (list,
search, download, preview, edit, share, delete) is authorized centrally in a new `DocumentService`
that follows the existing service-level IDOR-protection pattern. Files are served only through
authorized minimal-API endpoints. Three new entities (`Document`, `DocumentShare`,
`DocumentActivity`) are added to the existing EF Core model on SQL Server LocalDB.

## Technical Context

**Language/Version**: C# 12 / .NET 8.0 (`net8.0`, nullable enabled, implicit usings)

**Primary Dependencies**: ASP.NET Core 8.0 Blazor Server, Entity Framework Core 8.0 (SqlServer), Bootstrap 5.3 + Bootstrap Icons. No new runtime packages.

**Storage**: SQL Server LocalDB (metadata, shares, activity); local filesystem for file bytes under a configurable root (default `AppData/uploads`, outside `wwwroot`)

**Testing**: New xUnit test project `ContosoDashboard.Tests` (service-level tests using the EF Core InMemory provider, test-only) plus the manual two-user verification in [quickstart.md](quickstart.md)

**Target Platform**: Windows/Linux/macOS developer machines running Kestrel; browser client; offline

**Project Type**: Web application (single Blazor Server project, layered Models/Data/Services/Pages)

**Performance Goals**: Upload of 25 MB <= 30 s; document list (<= 500 docs) < 2 s; search < 2 s; preview < 3 s (spec FR-038/FR-039)

**Constraints**: Offline-only, no cloud SDKs; files never under `wwwroot`; `DocumentId` is `int`; category stored as text; `FileType` column 255 chars; no new database engine; no change to mock authentication

**Scale/Scope**: Training scale (4 seeded users, tens to hundreds of documents per user); about 3 new entities, 4 new services/interfaces, 4 new pages/components, 2 endpoints

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate | Pre-research | Post-design |
|-----------|------|--------------|-------------|
| I. Training-only, offline-first | No cloud/runtime external dependency; mock auth unchanged | PASS | PASS - local storage and a local signature-based scanner; no new packages |
| II. Infrastructure abstraction | Storage and scanning via DI interfaces; GUID path generated before DB insert | PASS (planned) | PASS - `IFileStorageService`, `IMalwareScanService`; path generated before save and insert; business logic references only interfaces |
| III. Security by default (NON-NEGOTIABLE) | `[Authorize]` on pages; service-level authorization; IDOR-safe; headers/cookies untouched | PASS (planned) | PASS - every `IDocumentService` method takes the acting user id and authorizes itself; endpoints require auth and return 404 for inaccessible documents; whitelist + magic-byte validation; no wwwroot exposure; security header middleware unchanged |
| IV. Layered architecture | Pages call services only; no `DbContext` in pages/endpoints | PASS (planned) | PASS - pages and endpoints depend on `IDocumentService`, `IDocumentReportService`; `DbContext` only in `Data` and services |
| V. Simplicity | New dependencies/patterns justified | PASS with note | PASS with note - see Complexity Tracking (test project) |
| Stack constraints | .NET 8, LocalDB, Bootstrap, cookie auth | PASS | PASS |
| Quality gates | Constitution Check in plan; clean build; seed data keeps a fresh DB runnable; two-user isolation check | PASS | PASS - see Complexity Tracking for seed-data note; two-user check is in quickstart |

No unjustified violations.

## Project Structure

### Documentation (this feature)

```text
specs/001-document-management/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── service-interfaces.md
│   ├── http-endpoints.md
│   └── ui-pages.md
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
ContosoDashboard/                        # existing web project
├── Models/
│   ├── Document.cs                      # NEW: Document, DocumentCategories constants
│   ├── DocumentShare.cs                 # NEW
│   ├── DocumentActivity.cs              # NEW: entity + DocumentActivityType enum
│   ├── Notification.cs                  # CHANGED: add DocumentShared, DocumentAdded types
│   └── TaskItem.cs                      # CHANGED: Documents navigation collection
├── Data/
│   └── ApplicationDbContext.cs          # CHANGED: DbSets, relationships, indexes
├── Services/
│   ├── IFileStorageService.cs           # NEW: interface
│   ├── LocalFileStorageService.cs       # NEW
│   ├── IMalwareScanService.cs           # NEW: interface and result types
│   ├── LocalSignatureScanService.cs     # NEW
│   ├── DocumentService.cs               # NEW: IDocumentService + implementation
│   ├── DocumentReportService.cs         # NEW: IDocumentReportService + implementation
│   ├── DashboardService.cs              # CHANGED: DocumentCount, recent documents
│   └── ProjectService.cs                # unchanged
├── Endpoints/
│   └── DocumentEndpoints.cs             # NEW: download/preview minimal API
├── Pages/
│   ├── Documents.razor                  # NEW: My Documents / Shared with Me / Search tabs, upload modal
│   ├── DocumentReports.razor            # NEW: Administrator reports
│   ├── TaskDetails.razor                # NEW: task detail view with documents section
│   ├── ProjectDetails.razor             # CHANGED: Documents section
│   ├── Index.razor                      # CHANGED: document count card, Recent Documents widget
│   ├── Tasks.razor                      # CHANGED: navigate to task details
│   └── Login.cshtml.cs                  # CHANGED: add Department claim
├── Shared/
│   ├── NavMenu.razor                    # CHANGED: Documents (and admin Reports) links
│   └── DocumentList.razor               # NEW: reusable table of documents with actions
├── Program.cs                           # CHANGED: DI registration, endpoint mapping
├── appsettings.json                     # CHANGED: FileStorage section
└── AppData/uploads/                     # runtime, git-ignored

ContosoDashboard.Tests/                  # NEW xUnit project (sibling of the web project)
├── ContosoDashboard.Tests.csproj
├── DocumentServiceAuthorizationTests.cs
├── DocumentServiceUploadTests.cs
├── DocumentServiceSharingTests.cs
├── LocalFileStorageServiceTests.cs
└── LocalSignatureScanServiceTests.cs
```

**Structure Decision**: Keep the single Blazor Server project and its existing Models / Data /
Services / Pages / Shared layering (Constitution IV). One new `Endpoints/` folder holds the two
HTTP endpoints that Blazor components cannot serve (authorized file download/preview). A sibling
`ContosoDashboard.Tests` project is added so the authorization matrix can be verified
automatically.

## Complexity Tracking

> Constitution Check has no unjustified violations. Notes below record judgement calls.

| Item | Why Needed | Simpler Alternative Rejected Because |
|------|------------|-------------------------------------|
| New `ContosoDashboard.Tests` project with `Microsoft.EntityFrameworkCore.InMemory` (test-only) | The access matrix (4 roles x view/edit/delete/share, IDOR) is the highest-risk part of the feature and is easy to regress | Manual testing alone cannot cover the role/ownership combinations repeatably. InMemory is used only in tests, not as an application database, so the "SQL Server LocalDB only" constraint holds for the app |
| No seeded `Document` rows | Constitution requires a fresh database to run end-to-end; it does. Categories are code constants and no lookup data is needed | Seed rows would point at files that do not exist on disk and would break download for a fresh checkout |
| `Documents.Tags` as one delimited text column instead of a tag table | Tags are only searched/displayed, never queried relationally | A `DocumentTag` table adds a join and a migration surface for no spec requirement |
