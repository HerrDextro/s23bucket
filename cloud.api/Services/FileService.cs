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
        private readonly IMongoCollection<BsonDocument> _fsFiles;
        public FileService(IMongoClient mongoClient, IOptions<MongoSettings> settings)
        {
            _db = mongoClient.GetDatabase(settings.Value.MainDatabase);
            _bucket = new GridFSBucket(_db);
            _fsFiles = _db.GetCollection<BsonDocument>("fs.files");
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
                return new ServiceResult<List<GridFSFileOutput>>(ResultReturn.BadRequest, "The path is malformed.", null);
            }

            //bool normalizeSuccess = TryNormalizePath(path, out string normalizedPath);
            var builder = Builders<GridFSFileInfo>.Filter;
            var filter = builder.Eq("metadata.ownerId", userId) &
                         builder.Eq("metadata.parent", successfulNormalizedParentPath);

            if (!await CheckIfParentIsDir(successfulNormalizedParentPath, userId)) //ToListAsync never returns null, so check that the folder exists
            {
                return new ServiceResult<List<GridFSFileOutput>>(ResultReturn.NotFound, null);
            }

            using var cursor = await _bucket.FindAsync(filter);
            var files = await cursor.ToListAsync();

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
            return new ServiceResult<List<GridFSFileOutput>>(ResultReturn.OK, null, outputFilesList);
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
                var filter2 = builder.Eq(x => x.Filename, pathElement) & //GridFS filename: the one name field that files AND folders both have
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
            if (!VirtualPath.TryNormalizeFileName(file.FileName, out string fileName))
            {
                return new ServiceResult<UploadResult>(ResultReturn.BadRequest, null);
            }
            string successfulNormalizedParentPath;

            if (VirtualPath.TryNormalizePath(destinationPath, out string normalizedParentPath))
            {
                successfulNormalizedParentPath = normalizedParentPath;
            }
            else
            {
                //throw new UnableToNormalizePathException("The path provided is malformed");
                return new ServiceResult<UploadResult>(ResultReturn.BadRequest, "The path provided is malformed", null);
            }

            if (await CheckIfParentIsDir(destinationPath, ownerId) == false)
            {
                return new ServiceResult<UploadResult>(ResultReturn.NotFound, "The path provided contains an nonexistant directory", null); //parent folder doesnt exist -> 404
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
            var result = await TryGetFileMetadataByPath(fullPath, ownerId); //metadata only: doesnt open a download stream that never gets closed
            if (result.result == ResultReturn.OK)
            {
                return new ServiceResult<UploadResult>(ResultReturn.Conflict, null);
            }
            using var stream = file.OpenReadStream();
            var fileOptions = new GridFSUploadOptions
            {
                /*Metadata = new BsonDocument //OPEN: later make into dedicated model?
                {
                    { "ownerId", ownerId },
                    { "parent", successfulNormalizedParentPath }, //if root this should be /
                    { "fileName", fileName }, //put outside of Metadata? //ts actually redundant
                    { "type", UploadType.File },
                    { "contentType", file.ContentType },
                    { "permission", permissions.ToBsonDocument() }
                }*/

                Metadata = new FileOrFolderMetadata
                {
                    OwnerId = ownerId,
                    Parent = successfulNormalizedParentPath,
                    Type = UploadType.File,
                    ContentType = file.ContentType,
                    Permissions = permissions

                }.ToBsonDocument()
            };
            var fileId = await _bucket.UploadFromStreamAsync(file.FileName, stream, fileOptions);
            return new ServiceResult<UploadResult>(ResultReturn.OK, null, new UploadResult(fileId.ToString()));

        }
        public async Task<ServiceResult<UploadResult>> UploadFolderAsync(string destinationPath, string folderName, string ownerId, string ownerName, Dictionary<string, FilePermissions> permissions) //currently susceptible to filename injections tho
        {
            //OPEN: folder with the same name as file? check

            string successfulNormalizedParentPath;
            if (VirtualPath.TryNormalizePath(destinationPath, out string normalizedParentPath))
            {
                successfulNormalizedParentPath = normalizedParentPath;
            }
            else
            {
                return new ServiceResult<UploadResult>(ResultReturn.BadRequest, "The path provided is malformed.", null);
            }

            if (await CheckIfParentIsDir(destinationPath, ownerId) == false)
            {
                return new ServiceResult<UploadResult>(ResultReturn.NotFound, "The path provided contains a file " + destinationPath, null); //parent folder doesnt exist -> 404
            }

            if (!VirtualPath.TryNormalizeFileName(folderName, out string normalizedFolderName))
            {
                return new ServiceResult<UploadResult>(ResultReturn.BadRequest, null);
            }

            //checking if a folder with the same name in the same dir maybe already exists
            var result = await GetDirectoryChildren(ownerId, successfulNormalizedParentPath);
            foreach (var child in result.Value)
            {
                if (child.Name == normalizedFolderName) { return new ServiceResult<UploadResult>(ResultReturn.Conflict, null, null); } //bother... forgot the second null
            }

            Stream emptyMemoryStream = Stream.Null;

            var fileOptions = new GridFSUploadOptions
            {
                Metadata = new FileOrFolderMetadata
                {
                    OwnerId = ownerId,
                    Parent = successfulNormalizedParentPath,
                    Type = UploadType.Dir,
                    Permissions = permissions

                }.ToBsonDocument()
            };
            var fileId = await _bucket.UploadFromStreamAsync(folderName, emptyMemoryStream, fileOptions); 



            return new ServiceResult<UploadResult>(ResultReturn.OK, null, new UploadResult(fileId.ToString()));
        }
        public async Task<ServiceResult<GridFSFileResult?>> TryGetFileById(string fileId, string userId) //change ts to implement the cleaner tuple without contentType
        {
            try
            {
                var builder = Builders<GridFSFileInfo>.Filter;
                if (!ObjectId.TryParse(fileId, out ObjectId objectId))
                {
                    return new ServiceResult<GridFSFileResult?>(ResultReturn.BadRequest, null, null); //not a valid id at all
                }

                //ObjectId.TryParse("6af0f7dabf79a6dc7d59a92c", out ObjectId id);
                //var fileId123 = new ObjectId("6af0f7dabf79a6dc7d59a92c");
                //var filter123 = builder.Eq(f => f.Id, id);
                var filter = builder.Eq("metadata.ownerId", userId) &
                             builder.Eq("metadata.type", UploadType.File) &
                             builder.Eq(f => f.Id, objectId);

                var fileInfo = await _bucket.Find(filter).FirstOrDefaultAsync();

                if (fileInfo == null)
                {
                    return new ServiceResult<GridFSFileResult?>(ResultReturn.NotFound, null); 
                }

                var stream = await _bucket.OpenDownloadStreamAsync(fileInfo.Id);

                var filename = stream.FileInfo.Filename;
                var contentType = fileInfo.Metadata.GetValue("contentType", "application/octet-stream").AsString;
                /*stream.FileInfo.Metadata?["contentType"]?.AsString
                                 ?? "application/octet-stream";*/

                return new ServiceResult<GridFSFileResult?>(ResultReturn.OK, null, new GridFSFileResult(stream, filename, contentType));
            }
            catch (GridFSFileNotFoundException)
            {
                return new ServiceResult<GridFSFileResult?>(ResultReturn.NotFound, null);
            }
        }
        public async Task<ServiceResult<GridFSFileResult?>> TryGetFileByPath(string fullPath, string userId) //OPEN: implement handling
        {
            if (!VirtualPath.TryNormalizePath(fullPath, out string normalizedFullPath))
            {
                return new ServiceResult<GridFSFileResult?>(ResultReturn.BadRequest, "The path provided is malformed.", null);
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
                         builder.Eq(x => x.Filename, results.fileName);
            try
            {
                // 2. Find the file info document matching your metadata
                var fileInfo = await _bucket.Find(filter).FirstOrDefaultAsync();

                if (fileInfo == null) { return new ServiceResult<GridFSFileResult?>(ResultReturn.NotFound, null); }

                // 3. Open the stream using the found file's unique _id
                var stream = await _bucket.OpenDownloadStreamAsync(fileInfo.Id);
                var contentType = fileInfo.Metadata.GetValue("contentType", "application/octet-stream").AsString; //indexer would throw for folders/old files without contentType

                return new ServiceResult<GridFSFileResult?>(ResultReturn.OK, null, new GridFSFileResult(stream, fileInfo.Filename, contentType));
            }
            catch (System.IO.FileNotFoundException)
            {
                return new ServiceResult<GridFSFileResult?>(ResultReturn.NotFound, null);
            }
        }
        public async Task<ServiceResult> TryDeleteFileById(ObjectId ObjId, string userId)
        {
            var builder = Builders<GridFSFileInfo>.Filter;
            var filter = builder.Eq("metadata.ownerId", userId) &
                         builder.Eq(x => x.Id, ObjId);
            var fileInfo = await _bucket.Find(filter).FirstOrDefaultAsync();
            if (fileInfo == null)
            {
                return new ServiceResult(ResultReturn.NotFound);
            }
            //check if its a folder and contains children
            var type = (UploadType)fileInfo.Metadata["type"].AsInt32;
            string parentPath = fileInfo.Metadata["parent"].AsString;

            if (type == UploadType.Dir)
            {
                //list the children of the folder ITSELF (its full path), not of the folder it lives in
                string folderPath = parentPath == "/" ? $"/{fileInfo.Filename}" : $"{parentPath}/{fileInfo.Filename}";
                var result = await GetDirectoryChildren(userId, folderPath);
                if (result.result != ResultReturn.OK || result.Value.Count > 0)
                {
                    return new ServiceResult(ResultReturn.Conflict); //not empty: deleting it would orphan its children
                }
            }
            await _bucket.DeleteAsync(fileInfo.Id);

            return new ServiceResult(ResultReturn.OK);
        }
        public async Task<ServiceResult> TryDeleteFileByPath(string path, string userId)
        {
            if (!VirtualPath.TryNormalizePath(path, out string normalizedPath))
            {
                return new ServiceResult(ResultReturn.BadRequest);
            }
            (string parent, string fileName) results = VirtualPath.SplitPath(normalizedPath);
            var builder = Builders<GridFSFileInfo>.Filter;
            var filter = builder.Eq("metadata.ownerId", userId) &
                         builder.Eq("metadata.parent", results.parent) &
                         builder.Eq(x => x.Filename, results.fileName);
            var fileInfo = await _bucket.Find(filter).FirstOrDefaultAsync();
            if (fileInfo == null)
            {
                return new ServiceResult(ResultReturn.NotFound);
            }
            return await TryDeleteFileById(fileInfo.Id, userId); //reuse: same "folder must be empty" check as delete by id
        }
        public async Task<ServiceResult<GridFSFileInfo>> TryGetFileMetadataByPath(string fullPath, string userId)
        {
            if (!VirtualPath.TryNormalizePath(fullPath, out string normalizedFullPath))
            {
                return new ServiceResult<GridFSFileInfo>(ResultReturn.BadRequest, null);
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
                         builder.Eq(x => x.Filename, results.fileName);
            try
            {
                var fileInfo = await _bucket.Find(filter).FirstOrDefaultAsync();

                if (fileInfo == null) { return new ServiceResult<GridFSFileInfo?>(ResultReturn.NotFound, null); }

                return new ServiceResult<GridFSFileInfo?>(ResultReturn.OK, null, fileInfo);
            }
            catch (System.IO.FileNotFoundException)
            {
                return new ServiceResult<GridFSFileInfo?>(ResultReturn.NotFound, null);
            }
        }
        public async Task<ServiceResult<UploadResult>> UpdateFileContentAsync(IFormFile? file, string destinationPath, string ownerId) //currently susceptible to filename injections tho
        {
            string fileName = file.FileName;
            string successfulNormalizedParentPath;

            if (VirtualPath.TryNormalizePath(destinationPath, out string normalizedParentPath))
            {
                successfulNormalizedParentPath = normalizedParentPath;
            }
            else
            {
                return new ServiceResult<UploadResult>(ResultReturn.BadRequest, "The path provided is malformed", null);
            }

            if (await CheckIfParentIsDir(destinationPath, ownerId) == false)
            {
                return new ServiceResult<UploadResult>(ResultReturn.NotFound, null); //parent folder doesnt exist -> 404
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
            
            //old way
            var resultingFileInfo = await TryGetFileMetadataByPath(fullPath, ownerId);
            if (resultingFileInfo.result != ResultReturn.OK) //OPEN do a better check later to ensure its really the same file
            {
                return new ServiceResult<UploadResult>(ResultReturn.NotFound, null);
            }
            ObjectId ObjectId = resultingFileInfo.Value.Id; //Mongo compatible

            if ((UploadType)resultingFileInfo.Value.Metadata["type"].AsInt32 == UploadType.Dir)
            {
                return new ServiceResult<UploadResult>(ResultReturn.Conflict, "Cannot replace directory with file"); //thats a folder, cant replace it with a file
            }
           

            //keep the old metadata (owner, parent, permissions, later sharing), only the contentType is new
            var metadata = resultingFileInfo.Value.Metadata.DeepClone().AsBsonDocument;
            metadata["contentType"] = file.ContentType;

            //1. upload the new content FIRST: if this fails, the old file is still there
            ObjectId newFileId;
            try
            {
                using var stream = file.OpenReadStream();
                newFileId = await _bucket.UploadFromStreamAsync(file.FileName, stream, new GridFSUploadOptions { Metadata = metadata });
            }
            catch
            {
                return new ServiceResult<UploadResult>(ResultReturn.Error, null);
            }

            //2. THEN delete the old one
            try
            {
                await _bucket.DeleteAsync(ObjectId);
            }
            catch
            {
                await _bucket.DeleteAsync(newFileId); //roll back so there arent 2 files with the same name
                return new ServiceResult<UploadResult>(ResultReturn.Error, null);
            }

            return new ServiceResult<UploadResult>(ResultReturn.OK, null, new UploadResult(newFileId.ToString()));
        }
        public async Task<ServiceResult<string>> UpdateFileMetadataByPath(string ownerId, string originPath, string targetParent, string targetFileName) //test this
        {
            if (!VirtualPath.TryNormalizePath(originPath, out string normalizedPath))
            {
                return new ServiceResult<string>(ResultReturn.BadRequest, null);
            }
            (string originParent, string originFileName) results = VirtualPath.SplitPath(normalizedPath);
            if (!VirtualPath.TryNormalizeFileName(targetFileName, out string normalizedTargetFileName))
            {
                return new ServiceResult<string>(ResultReturn.BadRequest, null);
            }
            if (await CheckIfParentIsDir(targetParent, ownerId) == false)
            {
                return new ServiceResult<string>(ResultReturn.NotFound, "The path provided contains an nonexistant directory", null); //parent folder doesnt exist -> 404
            }

            //find origin file and check if its a file or dir
            var filterBuilder = Builders<GridFSFileInfo>.Filter;
            var originFileFilter = filterBuilder.Eq(x => x.Filename, results.originFileName) &
             filterBuilder.Eq(x => x.Metadata["ownerId"], ownerId) &
             filterBuilder.Eq(x => x.Metadata["parent"], results.originParent);

            var originFileInfo = await _bucket.Find(originFileFilter).FirstOrDefaultAsync(); //not finding anything on PATCH test "move into missing folder"
            if (originFileInfo == null)
            {
                return new ServiceResult<string>(ResultReturn.NotFound, null);
            }
            if (originFileInfo.Metadata["type"] == UploadType.Dir)
            {
                return new ServiceResult<string>(ResultReturn.BadRequest, null);
            }

            //ensure no conlict with target dir
            var conflictCheckFilterBuilder = Builders<GridFSFileInfo>.Filter;
            var conflictCheckFilter = conflictCheckFilterBuilder.Eq(x => x.Filename, normalizedTargetFileName) &
             conflictCheckFilterBuilder.Eq(x => x.Metadata["ownerId"], ownerId) &
             conflictCheckFilterBuilder.Eq(x => x.Metadata["parent"], targetParent);

            var conflictFileInfo = await _bucket.Find(conflictCheckFilter).FirstOrDefaultAsync();
            if (conflictFileInfo != null)
            {
                return new ServiceResult<string>(ResultReturn.Conflict, null);
            }

            //add the updated metadata separately from removing the old
            var filter = Builders<BsonDocument>.Filter.Eq("_id", originFileInfo.Id);

            var addTargetMetadata = Builders<BsonDocument>.Update
             .Set("metadata.parent", targetParent)
             .Set("filename", targetFileName);
            var updateResult = await _fsFiles.UpdateOneAsync(filter, addTargetMetadata);

            if (updateResult.ModifiedCount > 0)
            {
                return new ServiceResult<string>(ResultReturn.OK, null);
            }
            return new ServiceResult<string>(ResultReturn.Error, null);
        }

         
    }
}
