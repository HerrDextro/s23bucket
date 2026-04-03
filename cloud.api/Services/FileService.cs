using cloud.api.Models;
using cloud.api.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using MongoDB.Bson;
using System.Net.Sockets;

namespace cloud.api.Services
{
    public class FileService
    {
        GridFSBucket _bucket;

        public FileService(IMongoClient mongoClient, IOptions<MongoSettings> settings)
        {
            _bucket = new GridFSBucket(mongoClient.GetDatabase(settings.Value.MainDatabase));
        }
        /*
        public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType)
        {
            var options = new GridFSUploadOptions
            {
                Metadata = new BsonDocument
                {
                    { "contentType", contentType }
                }
            };
            var fileId = await _bucket.UploadFromStreamAsync(fileName, fileStream, options);
            return fileId.ToString();
        }*/
        public async Task<string> UploadFileAsync(IFormFile file, string path, bool isPublic, string ownerId)
        {
            // 1. Open a stream from the IFormFile
            using var stream = file.OpenReadStream();

            Dictionary<string, string> Permissions = new Dictionary<string, string>();

            // 2. Pack your custom metadata
            var options = new GridFSUploadOptions
            {
                Metadata = new BsonDocument
                {
                    { "path", path },
                    { "isPublic", isPublic },
                    { "ownerId", ownerId },
                    { "contentType", file.ContentType },
                    { "permission", Permissions.ToBsonDocument() }
                }
            };

            // 3. Upload (This shatters it into chunks automatically!)
            var fileId = await _bucket.UploadFromStreamAsync(file.FileName, stream, options);

            return fileId.ToString();
        }
    }
}
