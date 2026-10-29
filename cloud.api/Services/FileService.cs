using cloud.api.Models;
using cloud.api.Settings;
using Microsoft.Extensions.Options;
using Microsoft.VisualBasic;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using System.Globalization;
using System.Linq.Expressions;
using System.Net.Mime;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static System.Net.WebRequestMethods;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace cloud.api.Services
{
    public class FileService
    {
        private GridFSBucket _bucket;
        private IMongoDatabase _db;
        public FileService(IMongoClient mongoClient, IOptions<MongoSettings> settings)
        {
            _db = mongoClient.GetDatabase(settings.Value.MainDatabase);
            _bucket = new GridFSBucket(_db);
        }
        public async Task<ServiceResult<List<GridFSFileOutput>>> GetDirectoryChildren(string userId, string parentPath) //takes path and lists everything inside the last element of the path IF that is a directory
        {
            string successfulNormalizedParentPath;
            if (VirtualPath.TryNormalizePath(parentPath, out string normalizedParentPath))
            {
                successfulNormalizedParentPath = normalizedParentPath;
            }
            else
            {
                return new ServiceResult<List<GridFSFileOutput>>(ResultReturn.BadRequest);
            }

            //bool normalizeSuccess = TryNormalizePath(path, out string normalizedPath);
            var builder = Builders<GridFSFileInfo>.Filter;
            var filter = builder.Eq("metadata.ownerId", userId) &
                         builder.Eq("metadata.parent", successfulNormalizedParentPath);

            using var cursor = await _bucket.FindAsync(filter);
            var files = await cursor.ToListAsync();

            if (files == null)
            {
                return new ServiceResult<List<GridFSFileOutput>>(ResultReturn.NotFound);
            }

            List<GridFSFileOutput> outputFilesList = new List<GridFSFileOutput>();
            foreach (var file in files)
            {
                var invalidType = file.Metadata["type"].AsInt32;
                UploadType uploadType = (UploadType)invalidType;
                outputFilesList.Add(new GridFSFileOutput(file.IdAsBsonValue.AsObjectId.ToString(), file.Filename, uploadType.ToString(), file.Length, file.UploadDateTime));
            }

            /*
            foreach (var file in files)
            {
                Console.WriteLine($"{file.Filename} -> {file.Metadata["path"]}");
            }
            */
            return new ServiceResult<List<GridFSFileOutput>>(ResultReturn.OK, outputFilesList);
        }
        public async Task<bool> CheckIfParentIsDir(string parentPath, string userId)
        {
            //need to normalize path/
            //always include userId anyways/
            //Check if full path (plus fileName) even exists in the first place)/
            //Check if each index of the path is of type directory

            if (!VirtualPath.TryNormalizePath(parentPath, out string normalizedParentPath))
            {
                return false;
            }
            var builder = Builders<GridFSFileInfo>.Filter;

            string[] pathElements = normalizedParentPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            //string lastElement = string.Empty;
            string pathUntilNow = "/";
            foreach (var pathElement in pathElements)
            {
                var filter2 = builder.Eq("metadata.folderName", pathElement) &
                        builder.Eq("metadata.parent", pathUntilNow) &
                        builder.Eq("metadata.ownerId", userId) &
                        builder.Eq("metadata.type", UploadType.Dir);
                using var cursor = await _bucket.FindAsync(filter2); //still really really need to freaking learn what a cursor is
                var files = await cursor.ToListAsync();
                if (files.Count == 0)
                {
                    return false;
                }

                if (pathUntilNow == "/")
                {
                    pathUntilNow = $"/{pathElement}";
                }
                else
                {
                    pathUntilNow = $"{pathUntilNow}/{pathElement}";
                }
            }
            return true;
        }
        public async Task<ServiceResult<UploadResult>> UploadFileAsync(IFormFile? file, string destinationPath, string ownerId, string ownerName, Dictionary<string, FilePermissions> permissions) //currently susceptible to filename injections tho
        {
            string fileName = file.FileName;
            string successfulNormalizedParentPath;

            if (VirtualPath.TryNormalizePath(destinationPath, out string normalizedParentPath))
            {
                successfulNormalizedParentPath = normalizedParentPath;
            }
            else
            {
                //throw new UnableToNormalizePathException("The path provided is malformed");
                return new ServiceResult<UploadResult>(ResultReturn.BadRequest);
            }

            if (await CheckIfParentIsDir(destinationPath, ownerId) == false)
            {
                //throw new ParentPathContainsFileException("The path provided contains a file " + destinationPath);
                return new ServiceResult<UploadResult>(ResultReturn.BadRequest);
            }

            //checking if the file maybe already exists
            string fullPath = string.Empty;
            if (successfulNormalizedParentPath == "/")
            {
                fullPath = $"/{fileName}";
            }
            else
            {
                fullPath = $"{successfulNormalizedParentPath}/{fileName}";
            }
            var result = await TryGetFileByPath(fullPath, ownerId);
            if (result.result == ResultReturn.OK)
            {
                return new ServiceResult<UploadResult>(ResultReturn.Conflict);
            }
            using var stream = file.OpenReadStream();
            var fileOptions = new GridFSUploadOptions
            {
                Metadata = new BsonDocument //OPEN: later make into dedicated model?
                {
                    { "ownerId", ownerId },
                    { "parent", successfulNormalizedParentPath }, //if root this should be /
                    { "fileName", fileName }, //put outside of Metadata? //ts actually redundant
                    { "type", UploadType.File },
                    { "contentType", file.ContentType },
                    { "permission", permissions.ToBsonDocument() }
                }
            };
            var fileId = await _bucket.UploadFromStreamAsync(file.FileName, stream, fileOptions);
            return new ServiceResult<UploadResult>(ResultReturn.OK, new UploadResult(fileId.ToString()));

        }
        public async Task<ServiceResult<UploadResult>> UploadFolderAsync(string destinationPath, string folderName, string ownerId, string ownerName, Dictionary<string, FilePermissions> permissions) //currently susceptible to filename injections tho
        {
            string successfulNormalizedParentPath;
            if (VirtualPath.TryNormalizePath(destinationPath, out string normalizedParentPath))
            {
                successfulNormalizedParentPath = normalizedParentPath;
            }
            else
            {
                //throw new UnableToNormalizePathException("The path provided is malformed");
                return new ServiceResult<UploadResult>(ResultReturn.BadRequest);
            }

            if (await CheckIfParentIsDir(destinationPath, ownerId) == false)
            {
                //throw new ParentPathContainsFileException("The path provided contains a file " + destinationPath);
                return new ServiceResult<UploadResult>(ResultReturn.BadRequest);
            }

            //checking if a folder with the same name in the same dir maybe already exists
            var result = await GetDirectoryChildren(ownerId, successfulNormalizedParentPath);
            foreach (var child in result.Value)
            {
                if (child.Name == folderName) { return new ServiceResult<UploadResult>(ResultReturn.Conflict); }
            }

            Stream emptyMemoryStream = Stream.Null;

            var fileOptions = new GridFSUploadOptions
            {
                Metadata = new BsonDocument //OPEN: later make into dedicated model?
                {
                    { "ownerId", ownerId },
                    { "parent", successfulNormalizedParentPath },
                    { "folderName", folderName },
                    { "type", UploadType.Dir },
                    { "permission", permissions.ToBsonDocument() }
                }
            };
            var fileId = await _bucket.UploadFromStreamAsync(folderName, emptyMemoryStream, fileOptions); //typo on purpose: need to implement parent is dir check in all endpoints
            return new ServiceResult<UploadResult>(ResultReturn.OK, new UploadResult(fileId.ToString()));
        }
        public async Task<ServiceResult<GridFSFileResult?>> TryGetFileById(string fileId, string userId) //change ts to implement the cleaner tuple without contentType
        {
            try
            {
                var builder = Builders<GridFSFileInfo>.Filter;
                ObjectId.TryParse(fileId, out ObjectId objectId);

                var filter = builder.Eq("metadata.ownerId", userId) &
                             builder.Eq(x => x.Id, objectId); //need to learn what this is
                var fileInfo = await _bucket.Find(filter).FirstOrDefaultAsync();

                if (fileInfo == null)
                {
                    //return (null, null, "400 Bad Request: Invalid Id");
                    return new ServiceResult<GridFSFileResult?>(ResultReturn.BadRequest);
                }

                var stream = await _bucket.OpenDownloadStreamAsync(fileInfo.Id);

                var filename = stream.FileInfo.Filename;
                var contentType = fileInfo.Metadata.GetValue("contentType", "application/octet-stream").AsString;
                    /*stream.FileInfo.Metadata?["contentType"]?.AsString
                                     ?? "application/octet-stream";*/

                return new ServiceResult<GridFSFileResult?>(ResultReturn.OK, new GridFSFileResult(stream, filename, contentType));
            }
            catch (GridFSFileNotFoundException)
            {
                return new ServiceResult<GridFSFileResult?>(ResultReturn.NotFound);
            }
        }
        public async Task<ServiceResult<GridFSFileResult?>> TryGetFileByPath(string fullPath, string userId) //OPEN: implement handling
        {
            if (!VirtualPath.TryNormalizePath(fullPath, out string normalizedFullPath))
            {
                return new ServiceResult<GridFSFileResult?>(ResultReturn.BadRequest);
            }
            (string parentPath, string fileName) results = VirtualPath.SplitPath(normalizedFullPath); //programmed at 3:12 am Btw
            /*
            if (await CheckIfParentIsDir(results.parentPath, userId)) //unneccesary
            {
                return new GridFSFileResult(ResultReturn.BadRequest, null, null, null);
            }*/
            var builder = Builders<GridFSFileInfo>.Filter;
            var filter = builder.Eq("metadata.ownerId", userId) &
                         builder.Eq("metadata.parent", results.parentPath) &
                         builder.Eq("metadata.fileName", results.fileName);
            try
            {
                // 2. Find the file info document matching your metadata
                var fileInfo = await _bucket.Find(filter).FirstOrDefaultAsync();

                if (fileInfo == null) { return new ServiceResult<GridFSFileResult?>(ResultReturn.NotFound); }

                // 3. Open the stream using the found file's unique _id
                var stream = await _bucket.OpenDownloadStreamAsync(fileInfo.Id);
                var contentType = stream.FileInfo.Metadata?["contentType"]?.AsString
                                     ?? "application/octet-stream";

                return new ServiceResult<GridFSFileResult?>(ResultReturn.OK, new GridFSFileResult(stream, fileInfo.Filename, contentType));
            }
            catch (System.IO.FileNotFoundException)
            {
                return new ServiceResult<GridFSFileResult?>(ResultReturn.NotFound);
            }
        }
        public async Task<ResultReturn> TryDeleteFileById(ObjectId ObjId, string userId)
        {
            var builder = Builders<GridFSFileInfo>.Filter;
            var filter = builder.Eq("metadata.ownerId", userId) &
                         builder.Eq(x => x.Id, ObjId);
            var fileInfo = await _bucket.Find(filter).FirstOrDefaultAsync();
            if (fileInfo == null)
            {
                return ResultReturn.NotFound;
            }
            //check if its a folder and contains children
            var type = (UploadType)fileInfo.Metadata["type"].AsInt32;
            string parentPath = fileInfo.Metadata["parent"].AsString;

            if (type == UploadType.Dir)
            {
                var result = await GetDirectoryChildren(userId, parentPath);
                if (result.result != ResultReturn.OK)
                {
                    return ResultReturn.Conflict;
                }
            }
            await _bucket.DeleteAsync(fileInfo.Id);

            return ResultReturn.OK;
        }
        public async Task<ResultReturn> TryDeleteFileByPath(string path, string userId)
        {
            (string parent, string fileName) results = VirtualPath.SplitPath(path);
            var builder = Builders<GridFSFileInfo>.Filter;
            var filter = builder.Eq("metadata.ownerId", userId) &
                         builder.Eq("metadata.parent", results.parent) &
                         builder.Eq("metadata.fileName", results.fileName);
            var fileInfo = await _bucket.Find(filter).FirstOrDefaultAsync();
            if (fileInfo == null)
            {
                return ResultReturn.NotFound;
            }
            await _bucket.DeleteAsync(fileInfo.Id); //file not found file id 1

            return ResultReturn.OK;
        }
        public async Task<ServiceResult<GridFSFileInfo>> TryGetFileMetaDataByPath(string fullPath, string userId)
        {
            if (!VirtualPath.TryNormalizePath(fullPath, out string normalizedFullPath))
            {
                return new ServiceResult<GridFSFileInfo>(ResultReturn.BadRequest);
            }
            (string parentPath, string fileName) results = VirtualPath.SplitPath(normalizedFullPath); //programmed at 3:12 am Btw
            /*
            if (await CheckIfParentIsDir(results.parentPath, userId)) //unneccesary
            {
                return new GridFSFileResult(ResultReturn.BadRequest, null, null, null);
            }*/
            var builder = Builders<GridFSFileInfo>.Filter;
            var filter = builder.Eq("metadata.ownerId", userId) &
                         builder.Eq("metadata.parent", results.parentPath) &
                         builder.Eq("metadata.fileName", results.fileName);
            try
            {
                var fileInfo = await _bucket.Find(filter).FirstOrDefaultAsync();

                if (fileInfo == null) { return new ServiceResult<GridFSFileInfo?>(ResultReturn.NotFound); }

                return new ServiceResult<GridFSFileInfo?>(ResultReturn.OK, fileInfo);
            }
            catch (System.IO.FileNotFoundException)
            {
                return new ServiceResult<GridFSFileInfo?>(ResultReturn.NotFound);
            }
        }
        public async Task<ServiceResult<UploadResult>> UpdateFileContentAsync(IFormFile? file, string destinationPath, string ownerId, string ownerName, Dictionary<string, FilePermissions> permissions) //currently susceptible to filename injections tho
        {
            string fileName = file.FileName;
            string successfulNormalizedParentPath;

            if (VirtualPath.TryNormalizePath(destinationPath, out string normalizedParentPath))
            {
                successfulNormalizedParentPath = normalizedParentPath;
            }
            else
            {
                //throw new UnableToNormalizePathException("The path provided is malformed");
                return new ServiceResult<UploadResult>(ResultReturn.BadRequest);
            }

            if (await CheckIfParentIsDir(destinationPath, ownerId) == false)
            {
                //throw new ParentPathContainsFileException("The path provided contains a file " + destinationPath);
                return new ServiceResult<UploadResult>(ResultReturn.BadRequest);
            }

            //checking if the file maybe already exists
            string fullPath = string.Empty;
            if (successfulNormalizedParentPath == "/")
            {
                fullPath = $"/{fileName}";
            }
            else
            {
                fullPath = $"{successfulNormalizedParentPath}/{fileName}";
            }
            var resultingFileInfo = await TryGetFileMetaDataByPath(fullPath, ownerId); //REALLY memory inefficient I think, fix by adding new internal method for metadata

            if (resultingFileInfo.result != ResultReturn.OK) //OPEN do a better chech later to ensure its really the same file
            {
                return new ServiceResult<UploadResult>(ResultReturn.NotFound);
            }
            ObjectId ObjectId = resultingFileInfo.Value.Id; //Mongo compatible

            try
            {
                await TryDeleteFileById(ObjectId, ownerId);
            }
            catch
            {
                return new ServiceResult<UploadResult>(ResultReturn.Error);
            }

            using var stream = file.OpenReadStream();
            var fileOptions = new GridFSUploadOptions
            {
                Metadata = new BsonDocument
                {
                    { "ownerId", ownerId },
                    { "parent", successfulNormalizedParentPath }, //if root this should be /
                    { "fileName", fileName }, //put outside of Metadata? No
                    { "type", UploadType.File },
                    { "contentType", file.ContentType },
                    { "permission", permissions.ToBsonDocument() }
                }
            };

            try
            {
                var fileId = await _bucket.UploadFromStreamAsync(file.FileName, stream, fileOptions);
                return new ServiceResult<UploadResult>(ResultReturn.OK, new UploadResult(fileId.ToString()));
            }
            catch
            {
                return new ServiceResult<UploadResult>(ResultReturn.Error);
            }
        }


        /*public async Task<ResultReturn> TryUpdateFileContentById(IFormFile file, string userId)
        {
            var builder = Builders<GridFSFileInfo>.Filter;
            ObjectId objectId = ObjectId.Parse(fileId);
            var filter = builder.Eq("metadata.ownerId", userId) &
                         builder.Eq(x => x.Id, objectId);
            var fileInfo = await _bucket.Find(filter).FirstOrDefaultAsync();
            if (fileInfo == null)
            {
                return ResultReturn.NotFound;
            }
            await _bucket
        }*/
    }
}
