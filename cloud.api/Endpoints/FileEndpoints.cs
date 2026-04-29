using System.Runtime.CompilerServices;
using System.Security.Claims;
using cloud.api.Services;

namespace cloud.api.Endpoints
{
    public static class FileEndpoints
    {
        public static void MapFileEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapPost("/files/upload", async (IFormFile file, ClaimsPrincipal userClaim, FileService fileService) =>
            {
                var user = userClaim.Identity?.Name;
                Dictionary<string, string> permissions = new Dictionary<string, string>
                {
                    { "user123", "read" },
                    { "user456", "write" }
                };

                var fileId = await fileService.UploadFileAsync(file, "/uploads", false, user, permissions);
                return Results.Ok(new { FileId = fileId });
            })
            .DisableAntiforgery()
            .RequireAuthorization();
        }
    }
}
