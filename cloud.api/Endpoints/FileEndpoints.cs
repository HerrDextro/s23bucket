using System.Runtime.CompilerServices;
using cloud.api.Services;

namespace cloud.api.Endpoints
{
    public static class FileEndpoints
    {
        public static void MapFileEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapPost("/files/upload", async (IFormFile file, FileService fileService) =>
            {
                var fileId = await fileService.UploadFileAsync(file, "/uploads", false, "owner123");
                return Results.Ok(new { FileId = fileId });
            })
            .DisableAntiforgery();
        }
    }
}
