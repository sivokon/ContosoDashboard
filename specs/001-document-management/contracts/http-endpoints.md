# Contract: HTTP Endpoints

Implemented as minimal API endpoints in `Endpoints/DocumentEndpoints.cs`, mapped from `Program.cs` after `UseAuthentication()` / `UseAuthorization()`. They call `IDocumentService` and never touch `DbContext` or the file system directly.

Common rules:
- `RequireAuthorization()` (cookie auth). Unauthenticated requests receive the existing login redirect/challenge behavior.
- The acting user id comes from the `NameIdentifier` claim; a missing or invalid claim returns `401`.
- Inaccessible and nonexistent documents are indistinguishable: both return `404` with an empty body (spec FR-031).
- Existing security-header middleware applies (including `X-Content-Type-Options: nosniff`); responses add `Cache-Control: private, no-store`.
- Safe methods only (`GET`), so antiforgery is not required.

## GET /api/documents/{id}/download

| Item | Value |
|---|---|
| Route parameter | `id` - integer `DocumentId` (non-integer -> `404`) |
| Success | `200` with the file bytes, `Content-Type` = stored `FileType`, `Content-Disposition: attachment; filename="<OriginalFileName>"` (filename encoded by the framework) |
| Side effect | `Download` activity recorded |
| Errors | `401` not signed in; `404` not found or no access; `500` file missing from storage (logged; generic message) |

## GET /api/documents/{id}/preview

| Item | Value |
|---|---|
| Route parameter | `id` - integer `DocumentId` |
| Success | `200` with `Content-Disposition: inline`, `Content-Type` = `application/pdf`, `image/jpeg`, or `image/png` |
| Side effect | `Preview` activity recorded |
| Errors | `401`; `404` not found or no access; `400` document type not previewable; `500` file missing from storage |

Notes:
- Preview of PDFs is opened by linking to this URL in a new tab; embedding in an `iframe` is blocked by the app's `X-Frame-Options: DENY` and is not supported. Images use an `<img src>` pointing at this URL.
- No endpoint exists for upload, edit, delete, or share. Those flows run through Blazor components calling `IDocumentService`, so they share the circuit's authenticated user.

## Configuration contract (`appsettings.json`)

```json
"FileStorage": {
  "RootPath": "AppData/uploads"
}
```

- Relative values resolve against the application content root; absolute values are used as is.
- The directory is created at startup if missing. The path MUST NOT be inside `wwwroot`; startup logs an error and refuses to use a root under `wwwroot`.
