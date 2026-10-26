using cloud.api.Models;
using cloud.api.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using System.Globalization;
using System.Linq.Expressions;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
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
        /*
         Path rules (static normalizer, no fields)
        Starts with /; root is /; no trailing / except root
        No empty segments, no . / .., no \
        Names: no /, no control chars, max 255
        Case-sensitive, stored as typed (no lowercasing)
        Invalid → 400, never "repair"
         */
        public bool TryNormalizePath(string? path, out string normalizedPath) //All AI btw
        {
            normalizedPath = string.Empty;

            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            // FormC (composed) is the usual choice for storing and comparing paths
            string normalized = path.Trim().Normalize(NormalizationForm.FormC);

            // Must be absolute
            if (normalized[0] != '/')
            {
                return false;
            }

            // No backslashes anywhere
            if (normalized.Contains('\\'))
            {
                return false;
            }

            // Root is valid on its own
            if (normalized == "/")
            {
                normalizedPath = "/";
                return true;
            }

            // Any other path must not end with '/'
            if (normalized[^1] == '/')
            {
                return false;
            }

            // Check each segment as a whole
            foreach (string segment in normalized.Substring(1).Split('/'))
            {
                if (segment.Length == 0 || segment == "." || segment == "..")
                {
                    return false;
                }
            }

            normalizedPath = normalized;
            return true;
        }
        public static (string parent, string name) SplitPath(string path)
        {
            int lastSlash = path.LastIndexOf('/');

            string name = path.Substring(lastSlash + 1);
            string parent = lastSlash == 0 ? "/" : path.Substring(0, lastSlash);

            return (parent, name);
        }
        public async Task<List<GridFSFileOutput>?> GetDirectoryChildren(string userId, string parentPath) //takes path and lists everything inside the last element of the path IF that is a directory
        {
            string successfulNormalizedParentPath;
            try
            {
                TryNormalizePath(parentPath, out string normalizedParentPath);
                successfulNormalizedParentPath = normalizedParentPath;
            }
            catch
            {
                throw new UnableToNormalizePathException("The path provided is malformed");
            }
            //bool normalizeSuccess = TryNormalizePath(path, out string normalizedPath);
            var builder = Builders<GridFSFileInfo>.Filter;
            var filter = builder.Eq("metadata.ownerId", userId) &
                         builder.Eq("metadata.parent", successfulNormalizedParentPath);

            using var cursor = await _bucket.FindAsync(filter);
            var files = await cursor.ToListAsync();
            List<GridFSFileOutput> outputFilesList = new List<GridFSFileOutput>();
            foreach (var file in files)
            {
                outputFilesList.Add(new GridFSFileOutput(file.IdAsBsonValue.AsObjectId.ToString(), file.Filename, file.GetType().Name, file.ChunkSizeBytes, file.UploadDateTime));
            }

            /*
            foreach (var file in files)
            {
                Console.WriteLine($"{file.Filename} -> {file.Metadata["path"]}");
            }
            */
            return outputFilesList;
        }
        public async Task<bool> CheckIfParentIsDir(string parentPath, string userId) //OPEN: Highly suspicious, check, CLOSED fixed 
        {
            //need to normalize path/
            //always include userId anyways/
            //Check if full path (plus fileName) even exists in the first place)/
            //Check if each index of the path is of type directory

            if (!TryNormalizePath(parentPath, out string normalizedParentPath))
            {
                return false;
            }
            var builder = Builders<GridFSFileInfo>.Filter;

            string[] pathElements = normalizedParentPath.Split('/', StringSplitOptions.RemoveEmptyEntries); 
            
            //string lastElement = string.Empty;
            string pathUntilNow = "/";
            foreach (var pathElement in pathElements ) 
            {
                var filter2 = builder.Eq("metadata.fileOrFolderName", pathElement) &
                        builder.Eq("metadata.parent", pathUntilNow) &
                        builder.Eq("metadata.ownerId", userId) &
                        builder.Eq("metadata.type", UploadType.Dir);
                using var cursor = await _bucket.FindAsync(filter2); //still really really need to freaking learn what a cursor is
                var files = await cursor.ToListAsync();
                if(files.Count == 0)
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
        public async Task<string> UploadFileAsync(IFormFile? file, string destinationPath, UploadType type, string ownerId, string ownerName, Dictionary<string, FilePermissions>permissions) //currently susceptible to filename injections tho
        {
            string fileName = file.FileName;
            string successfulNormalizedParentPath;
            try
            {
                TryNormalizePath(destinationPath, out string normalizedParentPath);
                successfulNormalizedParentPath = normalizedParentPath;
            }
            catch
            {
                throw new UnableToNormalizePathException("The path provided is malformed");
            }

            if (await CheckIfParentIsDir(destinationPath, ownerId) == false)
            {
                throw new ParentPathContainsFileException("The path provided contains a file " + destinationPath);
            }

            using var stream = file.OpenReadStream();
            var fileOptions = new GridFSUploadOptions
            {
                Metadata = new BsonDocument //OPEN: later make into dedicated model?
                {
                    { "ownerId", ownerId },
                    { "parent", successfulNormalizedParentPath }, //if root this should be /
                    { "fileOrFolderName", fileName }, //put outside of Metadata? No
                    { "type", type },
                    { "permission", permissions.ToBsonDocument() } 
                }
            };
            var fileId = await _bucket.UploadFromStreamAsync(file.FileName, stream, fileOptions);
            return fileId.ToString();
        }
        public async Task<string> UploadFolderAsync(string destinationPath, string folderName, string ownerId, string ownerName, Dictionary<string, FilePermissions> permissions) //currently susceptible to filename injections tho
        {
            string successfulNormalizedParentPath;
            try
            {
                TryNormalizePath(destinationPath, out string normalizedParentPath);
                successfulNormalizedParentPath = normalizedParentPath;
            }
            catch
            {
                throw new UnableToNormalizePathException("The path provided is malformed");
            }

            if (await CheckIfParentIsDir(destinationPath, ownerId) == false)
            {
                throw new ParentPathContainsFileException("The path provided contains a file " + destinationPath);
            }

            var ghostFile = Array.Empty<byte>();

            Stream emptyMemoryStream = Stream.Null;

            var fileOptions = new GridFSUploadOptions
            {
                Metadata = new BsonDocument //OPEN: later make into dedicated model?
                {
                    { "ownerId", ownerId },
                    { "parent", successfulNormalizedParentPath },
                    { "fileOrFolderName", folderName }, //put outside of Metadata? No
                    { "type", UploadType.Dir },
                    { "permission", permissions.ToBsonDocument() } 
                }
            };
            var fileId = await _bucket.UploadFromStreamAsync(folderName, emptyMemoryStream, fileOptions); //typo on purpose: need to implement parent is dir check in all endpoints
            return fileId.ToString();
        }
        public async Task<(Stream stream, string fileName, string httpResult)> TryGetFileById(string id, string userId) //change ts to implement the cleaner tuple without contentType
        {
            if (!ObjectId.TryParse(id, out var objectId))
            {
                return (null, null, "400 Bad Request: Invalid Id");
            }

            try
            {
                // Open the MongoDB download stream
                var stream = await _bucket.OpenDownloadStreamAsync(objectId);

                // Safely fetch metadata
                var filename = stream.FileInfo.Filename;
                /*var contentType = stream.FileInfo.Metadata?["ContentType"]?.AsString
                                     ?? "application/octet-stream";*/

                return (stream, filename, 200.ToString());
            }
            catch(GridFSFileNotFoundException)
            {
                return (null, null, "404");
            }   
        }//insignificant and useless and insecure
        public async Task<GridFSFileResult?> TryGetFileByPath(string fullPath, string userId) //OPEN: implement handling
        {
            bool success = TryNormalizePath(fullPath, out string normalizedFullPath);
            (string parentPath, string fileName) results = SplitPath(normalizedFullPath); //programmed at 3:12 am Btw

            var builder = Builders<GridFSFileInfo>.Filter;
            var filter = builder.Eq("metadata.ownerId", userId) &
                         builder.Eq("metadata.parent", results.parentPath) &
                         builder.Eq("metadata.fileOrFolderName", results.fileName);
            try
            {
                // 2. Find the file info document matching your metadata
                var fileInfo = await _bucket.Find(filter).FirstOrDefaultAsync();

                if (fileInfo == null)
                {
                    return null; // No file matched these attributes
                }

                // 3. Open the stream using the found file's unique _id
                var stream = await _bucket.OpenDownloadStreamAsync(fileInfo.Id);

                /*var contentType = fileInfo.Metadata?["ContentType"]?.AsString //sus ahh 
                                     ?? "application/octet-stream";*/

                return new GridFSFileResult(stream, fileInfo.Filename);
            }
            catch (System.IO.FileNotFoundException)
            {
                return null;
            }
        }     
    }

    public record GridFSFileResult
    (
        GridFSDownloadStream Stream,
        string Filename
        //string ContentType
    );
    //[{ id, name, type, size, uploadDate }]
    public class GridFSFileOutput
    {
        public GridFSFileOutput(string id, string name, string type, int size, DateTime uploadDate) //yes I know this is cursed as hell but I wasted enough time getting the google AI to tell me what the way is to populate the object in the foreach loop in GetDirectoryChildren()
        {
            Id = id;
            Name = name;
            Type = type;
            Size = size;
            UploadDate = uploadDate;

        }
        public string Id {  get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public int Size { get; set; }
        public DateTime UploadDate { get; set; }
    }

    public enum UploadType 
    {
        File,
        Dir
    }
    public enum FilePermissions
    {
        Read,
        ReadWrite
    }
    //these are AI tho
    public class UnableToNormalizePathException : Exception
    {
        public UnableToNormalizePathException(string message) : base(message) { }
    }

    public class ParentPathContainsFileException : Exception
    {
        public ParentPathContainsFileException(string message) : base(message) { }
    }
}
