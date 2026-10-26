using cloud.api.Dtos;
using cloud.api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver.GridFS;
using System.Net.Mime;
using System.Runtime.CompilerServices;
using System.Security.Claims;

namespace cloud.api.Endpoints
{
    public static class FileEndpoints
    {
        public static void MapFileEndpoints(this IEndpointRouteBuilder app)
        {
            //POST upload files 
            app.MapPost("/files", async ([AsParameters] FileUploadDto metadata, ClaimsPrincipal userClaim, FileService fileService) =>
            {
                if (metadata.File is null || metadata.File.Length == 0)
                {
                    return Results.BadRequest("No file was uploaded.");
                }

                var userName = userClaim.Identity?.Name;
                var userId = userClaim.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (userId == null) //ask big C abt this
                {
                    return Results.Text("UserId not found in claims");
                }

                var permissions = new Dictionary<string, FilePermissions>();
                permissions.Add(userId, FilePermissions.ReadWrite);

                var fileId = await fileService.UploadFileAsync(metadata.File, metadata.ParentPath, metadata.Type, userId, userName, permissions);

                string locationUri = Path.Combine(metadata.ParentPath, metadata.File.FileName); 

                return Results.Created(locationUri, new { FileId = fileId });
            })
            .DisableAntiforgery()
            .RequireAuthorization();
            
            //GET files by id
            app.MapGet("/files/{id}", async (string id, ClaimsPrincipal userClaim, FileService fileService, HttpContext context) => //always 404
            {
                var userId = userClaim.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var (stream, fileName, httpResult) = await fileService.TryGetFileById(id, userId);

                context.Response.Headers.Append("X-File-Owner-Id", userId);
                context.Response.Headers.Append("X-Upload-Type", "Invoice");

                return Results.File(stream, fileName);
            })
            .RequireAuthorization();

            //GET files by path 
            app.MapGet("/files", async (string path, ClaimsPrincipal userClaim, FileService fileService) => 
            {
                var userId = userClaim.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                GridFSFileResult? fileResult = await fileService.TryGetFileByPath(path, userId);

                if (fileResult == null)
                {
                    return Results.NotFound(new { message = "No file found" });
                }

                return Results.Stream(
                    fileResult.Stream,
                    //contentType: fileResult.ContentType,
                    fileDownloadName: fileResult.Filename
                );
            })
            .RequireAuthorization();

            //POST folder
            app.MapPost("/folders", async (FolderUploadDto metadata, ClaimsPrincipal userClaim, FileService fileService) => //return 201 and folder id? //OPEN: see if a chage to separated Dto's makes sense with the fileservice
            {
                var userName = userClaim.Identity?.Name;
                var userId = userClaim.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (userId == null) //ask big C abt this
                {
                    return Results.Text("UserId not found in claims");
                }

                var permissions = new Dictionary<string, FilePermissions>();
                permissions.Add(userId, FilePermissions.ReadWrite);

                var fileId = await fileService.UploadFolderAsync(metadata.ParentPath, metadata.FolderName, userId, userName, permissions);

                string locationUri = Path.Combine(metadata.ParentPath, metadata.FolderName);

                return Results.Created(locationUri, new { FileId = fileId });

            })
            .RequireAuthorization();

            //GET directory children by path (parent path)
            app.MapGet("/files/content", async (string path, ClaimsPrincipal userClaims, FileService fileService) =>
            {
                var userId = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var files = await fileService.GetDirectoryChildren(userId, path);
                return Results.Ok(files);
            })
            .RequireAuthorization();

        }
    }
}
