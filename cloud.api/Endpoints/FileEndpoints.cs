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
                //string locationUri = $"/files/{result.Value.uploadId}"; //Location = the URL where the new file can be fetched
                string locationUri = string.Empty;
                if (result.Value != null)
                {
                    locationUri = $"/files/{result.Value.uploadId}";
                }

                return ResultHelper(result, x => Results.Created(locationUri, new { FileId = result.Value.uploadId }));
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
                string locationUri = string.Empty;
                if (result.Value != null)
                {
                    locationUri = $"/files/{result.Value.uploadId}";
                }

                return ResultHelper(result, x => Results.Created(locationUri, new { FileId = result.Value.uploadId }));
            })
            .RequireAuthorization();

            //GET files by id
            app.MapGet("/files/{id}", async (string id, ClaimsPrincipal userClaim, FileService fileService, HttpContext context) =>
            {
                var userId = userClaim.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var result = await fileService.TryGetFileById(id, userId);

                return ResultHelper(result, x => Results.File(result.Value.Stream, result.Value.ContentType, result.Value.Filename));
            })
            .RequireAuthorization();

            //GET files by path 
            app.MapGet("/files", async (string path, ClaimsPrincipal userClaim, FileService fileService) => 
            {
                var userId = userClaim.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                ServiceResult<GridFSFileResult?> result = await fileService.TryGetFileByPath(path, userId);
                return ResultHelper(result, x => Results.File(result.Value.Stream, result.Value.ContentType, result.Value.Filename));
            })
            .RequireAuthorization();

            //GET directory children by path (parent path)
            app.MapGet("/files/content", async (string path, ClaimsPrincipal userClaims, FileService fileService) =>
            {
                var userId = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var result = await fileService.GetDirectoryChildren(userId, path);
                return ResultHelper(result, x => Results.Ok(result.Value)); //only the list, not the whole ServiceResult wrapper
            })
            .RequireAuthorization();

            //DELETE files by file id
            app.MapDelete("/files/delete/{id}", async (string id, ClaimsPrincipal userClaim, FileService fileService) =>
            {
                if (!ObjectId.TryParse(id, out ObjectId objectId)) //Parse throws on garbage input -> 500
                {
                    return Results.BadRequest("This is not a valid file id.");
                }
                var userId = userClaim.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var result = await fileService.TryDeleteFileById(objectId, userId);
                return ResultHelper(result, () => Results.NoContent());
            })
            .RequireAuthorization();

            //DELETE files by path
            app.MapDelete("/files/delete", async (string path, ClaimsPrincipal userClaim, FileService fileService) =>
            {
                var userId = userClaim.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var result = await fileService.TryDeleteFileByPath(path, userId);

                return ResultHelper(result, ()=> Results.NoContent()); //since I dont plan on a response body
            })
            .RequireAuthorization();

            //update file content by path (patch is for metadata only)
            app.MapPut("/files", async ([AsParameters] FileUploadDto metadata, ClaimsPrincipal userClaim, FileService fileService) =>
            {
                if (metadata.File is null || metadata.File.Length == 0)
                {
                    return Results.BadRequest("No file was uploaded.");
                }

                //var userName = userClaim.Identity?.Name;
                var userId = userClaim.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (userId == null) //ask big C abt this
                {
                    return Results.Unauthorized();
                }
                ServiceResult<UploadResult> result = await fileService.UpdateFileContentAsync(metadata.File, metadata.ParentPath, userId);

                return ResultHelper(result, success => Results.Ok(new { FileId = result.Value.uploadId })); //200 not 201: an existing file was replaced. Its id changed, so we send the new one
            })
            .DisableAntiforgery() //add everywhere because of the bearer token?
            .RequireAuthorization();

            //update file metadata by path (file only for now until child updating is implemented
            app.MapPatch("/files", async (FilePatchDto filePatchDto, ClaimsPrincipal userClaim, FileService fileService) => 
            { 
                var userId = userClaim.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var result = await fileService.UpdateFileMetadataByPath(userId, filePatchDto.OriginPath, filePatchDto.TargetParent, filePatchDto.TargetFileName);

                return ResultHelper(result, x => Results.NoContent());
            })
            .RequireAuthorization();
        }
        public static IResult ResultHelper<T>(ServiceResult<T> serviceResult, Func<T, IResult> onSuccess) //AI helped me with this //yeah no wait a second why do I even have the Resultreturn💀💀💀
        {
            return serviceResult.result switch
            {
                ResultReturn.OK => onSuccess(serviceResult.Value!),                         // 2xx, built by the endpoint
                ResultReturn.BadRequest => Results.BadRequest(serviceResult.message),         // 400
                ResultReturn.Forbidden => Results.Forbid(),                                   // 403
                ResultReturn.NotFound => Results.NotFound(serviceResult.message),             // 404
                ResultReturn.Conflict => Results.Conflict(serviceResult.message),             // 409
                ResultReturn.Error => Results.InternalServerError(serviceResult.message),     // 500
                _ => Results.InternalServerError()                                            // unhandled value = bug
            };
        }
        public static IResult ResultHelper(ServiceResult serviceResult, Func<IResult> onSuccess)
        {
            return serviceResult.result switch
            {
                ResultReturn.OK => onSuccess(),                         // 2xx, built by the endpoint
                ResultReturn.BadRequest => Results.BadRequest(serviceResult.message),         // 400
                ResultReturn.Forbidden => Results.Forbid(),                                   // 403
                ResultReturn.NotFound => Results.NotFound(serviceResult.message),             // 404
                ResultReturn.Conflict => Results.Conflict(serviceResult.message),             // 409
                ResultReturn.Error => Results.InternalServerError(serviceResult.message),     // 500
                _ => Results.InternalServerError()                                            // unhandled value = bug
            };
        }

        //usage of that monster
        //return ResultHelper(result, file => Results.File(file.Stream, file.ContentType, file.Filename));
        //return ResultHelper(result, upload => Results.Created($"/files/{upload.uploadId}", new { FileId = upload.uploadId }));
    }
}