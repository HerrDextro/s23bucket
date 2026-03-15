using MongoDB.Driver;
using cloud.api.Models;
using cloud.api.Settings;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;


namespace cloud.api.Services
{
    public class AuthService
    {
        
        IMongoCollection<RefreshToken> _refreshTokens;
        public AuthService(IMongoClient mongoClient, IOptions<MongoSettings> settings) 
        { 
            var database = settings.Value.UserDatabase;
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
                ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)), 
                Token = refreshToken
            };
        }
        public async Task StoreRefreshToken(RefreshToken token)
        {
            await _refreshTokens.InsertOneAsync(token);
        }

    }
}
