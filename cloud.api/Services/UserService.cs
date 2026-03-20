using cloud.api.Models;
using cloud.api.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using cloud.api.Dtos;

namespace cloud.api.Services
{
    public class UserService
    {
        IMongoCollection<User> _users;
        public UserService(IMongoClient mongoClient, IOptions<MongoSettings> settings) 
        { 
            var database = settings.Value.IdentityDatabase;
            _users = mongoClient.GetDatabase(database).GetCollection<User>("users");
        }    
        public async Task RegisterUser(User user)
        {
            await _users.InsertOneAsync(user);
        }

        public async Task<User?> LoginUser(UserDto userDto)
        {
            var user = await _users.Find(u => u.Username == userDto.username).FirstOrDefaultAsync();
            if(user == null || !BCrypt.Net.BCrypt.Verify(userDto.password, user.HashedPassword))
            {
                return null;
            }
            return user;
        }

        public async Task<User> GetUserById(string id)
        {
            return await _users.Find(u => u.Id == id).FirstOrDefaultAsync();
        }
    }
}
