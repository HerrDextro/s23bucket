using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace cloud.api.Models
{
    public class ExternalClient
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public string Name { get; set; }
        public string URL { get; set; }
        public string HashedApiKey { get; set; }
    }
}
