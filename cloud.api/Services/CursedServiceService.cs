using cloud.api.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using cloud.api.Settings;

namespace cloud.api.Services
{
    public class CursedServiceService
    {
        IMongoCollection<Service> _services;

        public CursedServiceService(IMongoClient mongoClient, IOptions<MongoSettings> settings)
        {
            var database = settings.Value.ServiceDatabase;
            _services = mongoClient.GetDatabase(database).GetCollection<Service>("services");
        }
        public async Task StoreService(Service service)
        {
            await _services.InsertOneAsync(service);
        }

        public async Task<List<Service>> GetAllServices()
        {
            return await _services.Find(k => true).ToListAsync();
        }

        public async Task<bool> CheckServiceApiKey(string apiKey)
        {
            var service = await _services.Find(k => k.ApiKey == apiKey).FirstOrDefaultAsync();  
            if(service != null) { return true;  }
            return false;
        }
    }
}
