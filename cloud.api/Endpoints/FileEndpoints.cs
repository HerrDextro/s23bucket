using cloud.api.Dtos;
using cloud.api.Models;
using cloud.api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver.GridFS;
using System.Net.Mime;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using static System.Net.WebRequestMethods;

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
            }) //find a way to put 507 in ts
            .DisableAntiforgery() //add everywhere because of the bearer token?
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
            app.MapGet("/files/{id}", async (string id, ClaimsPrincipal userClaim, FileService fileService, HttpContext context) =>
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
                var result = await fileService.GetDirectoryChildren(userId, path);
                if (result.result == ResultReturn.BadRequest)
                {
                    return Results.BadRequest("Invalid parent path provided");
                }
                if (result.result == ResultReturn.NotFound)
                {
                    return Results.NotFound();
                }
                return Results.Ok(result);
            })
            .RequireAuthorization();

            //DELETE files by file id
            app.MapDelete("/files/delete/{id}", async (string id, ClaimsPrincipal userClaim, FileService fileService) =>
            {
                ObjectId objectId = ObjectId.Parse(id);
                var userId = userClaim.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var result = await fileService.TryDeleteFileById(objectId, userId);
                if (result == ResultReturn.NotFound)
                {
                    return Results.NotFound();
                }
                return Results.NoContent(); //since I dont plan on a response body
            })
            .RequireAuthorization();

            //DELETE files by path
            app.MapDelete("/files/delete", async (string path, ClaimsPrincipal userClaim, FileService fileService) =>
            {
                var userId = userClaim.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var result = await fileService.TryDeleteFileByPath(path, userId);
                if (result == ResultReturn.NotFound)
                {
                    return Results.NotFound();
                }
                return Results.NoContent(); //since I dont plan on a response body
            })
            .RequireAuthorization();

            //update file content by path (patch is for metadata only)
            app.MapPut("/files", async ([AsParameters] FileUploadDto metadata, ClaimsPrincipal userClaim, FileService fileService) =>
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

                ServiceResult<UploadResult> result = await fileService.UpdateFileContentAsync(metadata.File, metadata.ParentPath, userId, userName, permissions);
                if (result.result == ResultReturn.BadRequest)
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
                if (result.result == ResultReturn.Error)
                {
                    return Results.InternalServerError("Cannot upload file to storage");
                }

                string locationUri = $"{metadata.ParentPath}/{metadata.File.FileName}";
                return Results.Created(locationUri, new { FileId = result.Value.uploadId });
            })
            .DisableAntiforgery() //add everywhere because of the bearer token?
            .RequireAuthorization();
        }
    }
}
