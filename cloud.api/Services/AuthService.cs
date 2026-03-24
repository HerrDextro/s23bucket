using cloud.api.Dtos;
using cloud.api.Models;
using cloud.api.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;
using System.Security.Cryptography;


namespace cloud.api.Services
{
    public class AuthService //NOTE: hashing refreshtokens?
    {
        
        IMongoCollection<RefreshToken> _refreshTokens;
        public AuthService(IMongoClient mongoClient, IOptions<MongoSettings> settings) 
        { 
            var database = settings.Value.IdentityDatabase;
            _refreshTokens = mongoClient.GetDatabase(database).GetCollection<RefreshToken>("refreshTokens"); //need to test this
        }
        public async Task<RefreshToken> GenerateRefreshToken(User user)
        {
            var randNum = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randNum);
            string refreshToken = Convert.ToBase64String(randNum);
            return new RefreshToken
            {
                UserId = user.Id, 
                ExpiryDate = DateTime.UtcNow.AddDays(7),
                Token = refreshToken,
                IsRevoked = false
            };
        }
        public async Task StoreRefreshToken(RefreshToken token)
        {
            await _refreshTokens.InsertOneAsync(token);
        }
        public async Task<RefreshToken> GetRefreshTokenByToken(string token)
        {
            return await _refreshTokens.Find(t => t.Token == token).FirstOrDefaultAsync();
        }
        public async Task<RefreshToken> GetRefreshTokenByUserId(string userId)
        {
            return await _refreshTokens.Find(t => t.UserId == userId).FirstOrDefaultAsync();
        }
    }
}
