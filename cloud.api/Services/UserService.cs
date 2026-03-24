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
            _users = mongoClient.GetDatabase(database).GetCollection<User>("users"); //here error db name
        }    
        public async Task RegisterUser(UserDto userDto)
        {
            var newUser = new User
            {
                Username = userDto.username,
                HashedPassword = BCrypt.Net.BCrypt.HashPassword(userDto.password)
            };
            await _users.InsertOneAsync(newUser);
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

        public async Task<User> GetUserByUsername(string username)
        {
            return await _users.Find(u => u.Username == username).FirstOrDefaultAsync();
        }

        public async Task<User> GetUserById(string id)
        {
            return await _users.Find(u => u.Id == id).FirstOrDefaultAsync();
        }
    }
}
