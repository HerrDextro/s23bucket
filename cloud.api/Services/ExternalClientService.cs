using cloud.api.Dtos;
using cloud.api.Models;
using cloud.api.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace cloud.api.Services
{
    public class ExternalClientService
    {
        IMongoCollection<Models.ExternalClient> _services;

        public ExternalClientService(IMongoClient mongoClient, IOptions<MongoSettings> settings)
        {
            var database = settings.Value.ServiceDatabase;
            _services = mongoClient.GetDatabase(database).GetCollection<Models.ExternalClient>("services");
        }
        public async Task StoreService(Dtos.ExternalClientDto serviceDto)
        {
            var newService = (new Models.ExternalClient
            {
                Name = serviceDto.name,
                URL = serviceDto.url,
                HashedApiKey = BCrypt.Net.BCrypt.HashPassword(serviceDto.api_key)
            });
            await _services.InsertOneAsync(newService);
        }

        public async Task<List<Models.ExternalClient>> GetAllServices()
        {
            return await _services.Find(k => true).ToListAsync();
        }

        public async Task<bool> CheckServiceApiKey(string apiKey) //later: check if access to resource
        {
            var service = await _services.Find(k => k.HashedApiKey == apiKey).FirstOrDefaultAsync();  
            if(service != null) { return true;  }
            return false;
        }
    }
}
