# Quickstart: Validating Document Upload and Management

Validation guide for the finished feature. Details live in [data-model.md](data-model.md) and [contracts/](contracts/); full implementation is in `tasks.md` and the code.

## Prerequisites

- .NET 8 SDK, SQL Server LocalDB, a browser.
- Repository checked out on branch `001-document-management`.
- Sample files: a small PDF, a PNG, a `.docx`, a `.txt`, a file over 25 MB, a `.exe` (or any renamed unsupported file), and a text file containing the EICAR test string (the standard 68-character antivirus test string; harmless).

## One-time database reset

The app uses `EnsureCreated()`, so new tables only appear on a fresh database:

```powershell
sqllocaldb stop mssqllocaldb
sqllocaldb delete mssqllocaldb
```

(or `dotnet ef database drop --force` from the web project directory). The database is recreated on the next run. Also delete `ContosoDashboard/AppData/uploads` if it exists.

## Build and run

```powershell
dotnet build ContosoDashboard/ContosoDashboard.csproj
dotnet test ContosoDashboard.Tests/ContosoDashboard.Tests.csproj
dotnet run --project ContosoDashboard/ContosoDashboard.csproj
```

Expected: build with no new warnings, all tests pass, site at the URL printed by `dotnet run`. Log in at `/login` (no password).

Seeded users: `admin@contoso.com` (Administrator, IT), `camille.nicole@contoso.com` (Project Manager of the sample project), `floris.kregel@contoso.com` (Team Lead, project member), `ni.kang@contoso.com` (Employee, project member).

## Scenario 1: Upload and validation (spec story 1)

1. Log in as Ni Kang; open **Documents**; start an upload.
2. Choose the PDF; enter title and category; upload. Expect progress, then a success message, and the file appears in My Documents with correct size/type/date/uploader.
3. Try the file over 25 MB, the `.exe`, an empty file, and the EICAR file. Expect a clear rejection for each and no new rows or files under `AppData/uploads`.
4. Select two files at once. Expect shared category/project/tags and one editable title per file.
5. Rename a non-PDF to `.pdf` and upload. Expect rejection (signature mismatch).

## Scenario 2: Browse, sort, filter (story 2)

Upload several documents with different categories, projects, and dates; verify each sort option and each filter (alone and combined), and the empty state.

## Scenario 3: Download and preview (story 3)

1. Download the PDF and compare bytes with the original (hash). Preview the PDF in a new tab and the PNG inline.
2. Copy the download URL, log out, log in as `admin` (allowed) and as a user without access (expect `404`). Try a nonexistent id (expect `404`).

## Scenario 4: Role-based access (story 4) - two-user check (Constitution III)

1. As Ni Kang upload a "Personal Files" document (no project) and a "Reports" document for the sample project.
2. As Floris Kregel (Team Lead, same department): Search shows the project document (he is a project member). The Personal Files document must NOT appear. He cannot edit or delete Ni's documents.
3. As Camille Nicole (project manager): sees the project document in the project view and can delete it; cannot see the Personal Files document.
4. As admin: can view, download, edit metadata, and delete any document; actions appear in the activity record.
5. Confirm a user not in the project cannot see the project document by URL or search.

## Scenario 5: Search (story 5)

Search by title, description, tag, uploader name, and project name; confirm results are access-filtered and return in under 2 seconds with about 500 documents (seed with a script or repeated uploads).

## Scenario 6: Edit, replace, delete (story 6)

Edit metadata; replace the file with a new PDF (download returns the new file; old file gone from `AppData/uploads`); delete with cancel and with confirm (record and file removed).

## Scenario 7: Sharing (story 7)

As Ni Kang share a document with Floris. As Floris: a notification appears, the document is in Shared with Me, download works, and edit/delete/re-share are not offered. Revoke the share as Ni; access disappears.

## Scenario 8: Integration (story 8)

Open a task at `/tasks/{id}`; upload a document from it and attach an existing one; confirm project association. Check the dashboard Documents count and Recent Documents (last 5). Upload to the sample project as Ni and confirm Camille and Floris get a notification.

## Scenario 9: Activity and reports (story 9)

Perform upload, download, preview, edit, replace, share, revoke, delete. As admin open `/documents/reports`; confirm the three reports and that each action is recorded. Confirm non-admins get no access to the reports page or data.

## Scenario 10: Failure safety

- Stop the scan path (make the scanner throw, for example via a test double) and confirm uploads are rejected with "try again later" and nothing is stored.
- Make the storage root read-only and confirm the upload fails with a message and no database row is created.

## Performance spot checks

- 25 MB upload completes within 30 seconds locally.
- List with 500 documents loads in under 2 seconds; search under 2 seconds; PDF preview under 3 seconds.

## Done criteria

Every scenario behaves as described, `dotnet test` passes, and no files exist under `wwwroot` for uploads.
