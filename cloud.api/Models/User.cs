using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace cloud.api.Models
{
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public string Username { get; set; }
        public string HashedPassword { get; set; }
    }
}

