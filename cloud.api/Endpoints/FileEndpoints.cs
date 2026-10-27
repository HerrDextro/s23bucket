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
                    return Results.Unauthorized();
                }

                var permissions = new Dictionary<string, FilePermissions>();
                permissions.Add(userId, FilePermissions.ReadWrite);

                ServiceResult<UploadResult> result = await fileService.UploadFileAsync(metadata.File, metadata.ParentPath, userId, userName, permissions);
                if(result.result == ResultReturn.BadRequest)
                {
                    return Results.BadRequest("Unable to normalize Path. The Filepath might be malformed."); //polite ahh return
                }
                if (result.result == ResultReturn.NotFound)
                {
                    return Results.NotFound("Parent path contains inexistant parent directory");
                }
                if (result.result == ResultReturn.Conflict)
                {
                    return Results.Conflict("File with the same filename already eixsts");
                }

                string locationUri = $"{metadata.ParentPath}/{metadata.File.FileName}";
                return Results.Created(locationUri, new { FileId = result.Value.uploadId });
            })
            .DisableAntiforgery()
            .RequireAuthorization();

            //POST folder
            app.MapPost("/folders", async (FolderUploadDto metadata, ClaimsPrincipal userClaim, FileService fileService) => //return 201 and folder id? //OPEN: see if a chage to separated Dto's makes sense with the fileservice
            {
                var userName = userClaim.Identity?.Name;
                var userId = userClaim.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (userId == null) //ask big C abt this
                {
                    return Results.Unauthorized();
                }

                var permissions = new Dictionary<string, FilePermissions>();
                permissions.Add(userId, FilePermissions.ReadWrite);

                ServiceResult<UploadResult> result = await fileService.UploadFolderAsync(metadata.ParentPath, metadata.FolderName, userId, userName, permissions);
                if (result.result == ResultReturn.BadRequest)
                {
                    return Results.BadRequest("Unable to normalize Path. The Filepath might be malformed."); //polite ahh return
                }
                if (result.result == ResultReturn.NotFound)
                {
                    return Results.NotFound("Parent path contains inexistant parent directory");
                }
                if(result.result == ResultReturn.Conflict)
                {
                    return Results.Conflict("Folder with the same foldername already eixsts");
                }

                string locationUri = $"{metadata.ParentPath}/{metadata.FolderName}";


                return Results.Created(locationUri, new { FileId = result.Value.uploadId });

            })
            .RequireAuthorization();

            //GET files by id
            app.MapGet("/files/{id}", async (string id, ClaimsPrincipal userClaim, FileService fileService, HttpContext context) => //always 404
            {
                var userId = userClaim.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var result = await fileService.TryGetFileById(id, userId);

                if (result.result == ResultReturn.BadRequest)
                {
                    return Results.BadRequest("Unable to normalize Path. The Filepath might be malformed."); //polite ahh return
                }
                if (result.result == ResultReturn.NotFound)
                {
                    return Results.NotFound("Parent path contains inexistant parent directory");
                }

                return Results.File(result.Value.Stream, result.Value.Filename, result.Value.ContentType);
            })
            .RequireAuthorization();

            //GET files by path 
            app.MapGet("/files", async (string path, ClaimsPrincipal userClaim, FileService fileService) => 
            {
                var userId = userClaim.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                ServiceResult<GridFSFileResult?> result = await fileService.TryGetFileByPath(path, userId);

                if (result.result == ResultReturn.BadRequest)
                {
                    return Results.BadRequest("Unable to normalize Path. The Filepath might be malformed."); //polite ahh return
                }
                if (result.result == ResultReturn.NotFound)
                {
                    return Results.NotFound("Parent path contains inexistant parent directory");
                }

                return Results.File(result.Value.Stream, result.Value.Filename, result.Value.ContentType);
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
