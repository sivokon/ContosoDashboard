using System.Security.Claims;
using Microsoft.Net.Http.Headers;
using ContosoDashboard.Services;

namespace ContosoDashboard.Endpoints;

public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/documents").RequireAuthorization();

        group.MapGet("/{id:int}/download", (int id, ClaimsPrincipal user, IDocumentService documents, ILoggerFactory logger) =>
            ServeAsync(id, user, documents, logger, inline: false));

        group.MapGet("/{id:int}/preview", (int id, ClaimsPrincipal user, IDocumentService documents, ILoggerFactory logger) =>
            ServeAsync(id, user, documents, logger, inline: true));

        return app;
    }

    private static async Task<IResult> ServeAsync(
        int id, ClaimsPrincipal user, IDocumentService documents, ILoggerFactory loggerFactory, bool inline)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            return Results.Unauthorized();

        try
        {
            if (inline)
            {
                // Inaccessible documents stay indistinguishable from missing ones (404); only viewable but
                // non-previewable documents get a 400
                var item = await documents.GetDocumentAsync(id, userId);
                if (item == null)
                    return Results.NotFound();
                if (!item.CanPreview)
                    return Results.BadRequest("This file type cannot be previewed.");
            }

            var content = inline
                ? await documents.OpenForPreviewAsync(id, userId)
                : await documents.OpenForDownloadAsync(id, userId);

            return content == null ? Results.NotFound() : new DocumentFileResult(content, inline);
        }
        catch (FileNotFoundException)
        {
            return Results.Problem("The document could not be retrieved.", statusCode: StatusCodes.Status500InternalServerError);
        }
        catch (Exception ex)
        {
            loggerFactory.CreateLogger("DocumentEndpoints").LogError(ex, "Error serving document {DocumentId}", id);
            return Results.Problem("The document could not be retrieved.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private sealed class DocumentFileResult : IResult
    {
        private readonly DocumentContent _content;
        private readonly bool _inline;

        public DocumentFileResult(DocumentContent content, bool inline)
        {
            _content = content;
            _inline = inline;
        }

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            var disposition = new ContentDispositionHeaderValue(_inline ? "inline" : "attachment");
            disposition.SetHttpFileName(_content.FileName);

            var response = httpContext.Response;
            response.ContentType = _content.ContentType;
            response.ContentLength = _content.Length;
            response.Headers.ContentDisposition = disposition.ToString();
            response.Headers.CacheControl = "private, no-store";

            await using (_content.Content)
            {
                await _content.Content.CopyToAsync(response.Body, httpContext.RequestAborted);
            }
        }
    }
}
