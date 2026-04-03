using cloud.api.Dtos;
using cloud.api.Interfaces;
using cloud.api.Models;
using cloud.api.Settings;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Collections.Concurrent;

namespace cloud.api.Services
{
    public class ExternalClientService : IRequestTracker
    {
        IMongoCollection<ExternalClient> _services;
        MemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

        private readonly ConcurrentDictionary<string, ResourceRequest> _resourceRequest = new(); //concurrent because Lists arent thread safe

        public ExternalClientService(IMongoClient mongoClient, IOptions<MongoSettings> settings)
        {
            var database = settings.Value.ServiceDatabase;
            _services = mongoClient.GetDatabase(database).GetCollection<ExternalClient>("services");
        }
        public async Task StoreService(ExternalClientDto serviceDto)
        {
            var newService = (new Models.ExternalClient
            {
                Name = serviceDto.name,
                URL = serviceDto.url,
                HashedApiKey = BCrypt.Net.BCrypt.HashPassword(serviceDto.api_key)
            });
            await _services.InsertOneAsync(newService);
        }

        public async Task<List<ExternalClient>> GetAllServices()
        {
            return await _services.Find(k => true).ToListAsync();
        }

        public async Task<ExternalClient?> GetServiceByName(string name)
        {
            return await _services.Find(k => k.Name == name).FirstOrDefaultAsync();
        }

        public async Task<bool> CheckServiceApiKey(string serviceName, string apiKey) //later: check if access to resource
        {
            var service = await _services.Find(k => k.Name == serviceName).FirstOrDefaultAsync();
            if (service != null && BCrypt.Net.BCrypt.Verify(service.HashedApiKey, apiKey)) { return true; }
            return false;
        }

        public string StoreResourceRequest(Dictionary<string, string> permission)
        {
            var id = Guid.NewGuid().ToString();
            _cache.Set(id,  new ResourceRequest { Permissions = permission }, new MemoryCacheEntryOptions //CRITICAL, only ghosts, doesnt actually dispose
            {
                SlidingExpiration = TimeSpan.FromMinutes(1)
            });
            return id;
        }
        public Dictionary<string, string>? GetStoredResourceRequest(string id)
        {
            var storedRequest = _cache.TryGetValue(id, out ResourceRequest? resourceRequest);

            if (resourceRequest == null) { return null; }
            return resourceRequest.Permissions;
        }


    }
}
