using MongoDB.Bson.Serialization.Attributes;

namespace cloud.api.Models
{
    [BsonIgnoreExtraElements]
    public class FileOrFolderMetadata
    {
        [BsonElement("ownerId")]
        public string OwnerId { get; set; }
        [BsonElement("parent")]
        public string Parent {  get; set; }
        [BsonElement("type")]
        public UploadType Type { get; set; }
        [BsonElement("contentType")]
        public string? ContentType   { get; set; }
        [BsonElement("permissions")]
        public Dictionary<string, FilePermissions> Permissions { get; set; }
    }
}
/*
 TO read into model: BsonSerializer.Deserialize<FileMetadata>(info.Metadata). To write it: metadata.ToBsonDocument()
 */