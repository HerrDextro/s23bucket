using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace cloud.api.Models
{
    public class Service
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public string Name { get; set; }
        public string URL { get; set; }
        public string ApiKey { get; set; }
    }
}
