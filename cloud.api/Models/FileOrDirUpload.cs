using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace cloud.api.Models
{
    public class FileOrDirUpload
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Path { get; set; }
        public string OnwerId { get; set; }
        public bool IsPublic { get; set; }
        public string? ContentType { get; set; }
        public Dictionary<string, string> Permissions { get; set; }
    }
}