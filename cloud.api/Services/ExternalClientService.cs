using cloud.api.Dtos;
using cloud.api.Interfaces;
using cloud.api.Models;
using cloud.api.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Collections.Concurrent;

namespace cloud.api.Services
{
    public class ExternalClientService : IRequestTracker
    {
        IMongoCollection<Models.ExternalClient> _services;
        //IMongoCollection<Models.ResourceRequest> _resourceRequests;
        private readonly ConcurrentDictionary<string, ResourceRequest> _resourceRequest = new(); //concurrent because Lists arent thread safe

        public ExternalClientService(IMongoClient mongoClient, IOptions<MongoSettings> settings)
        {
            var database = settings.Value.ServiceDatabase;
            _services = mongoClient.GetDatabase(database).GetCollection<Models.ExternalClient>("services");
            //_resourceRequests = mongoClient.GetDatabase(database).GetCollection<Models.ResourceRequest>("services"); //watch out, several collections per DB allowed
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

        //DB is cleaner, but speed and SSD wear. 
        /*
        public async Task<string> StoreResourceRequest(Dictionary<string, string> resourceIdPermission) 
        { 
            var newRequest = new ResourceRequest
            {
                Id = Guid.NewGuid().ToString(),
                resource_id_permission = resourceIdPermission
            };
            await _resourceRequests.InsertOneAsync(newRequest);
            return newRequest.Id;
        }

        public async Task<ResourceRequest> GetStoredResourceRequest(string requestId) //here: return the whole request or only dictionary with permissions
        { 
            return await _resourceRequests.Find(k => k.Id == requestId).FirstOrDefaultAsync();
        }
        */
        public async Task<string> StoreResourceRequest(Dictionary<string, string> permission)
        {
            var id = Guid.NewGuid().ToString();
            _resourceRequest.TryAdd(id, new ResourceRequest { Creation = DateTime.Now, Permissions = permission }); //Bit cursed innit

            return id;
        }
        public Dictionary<string, string>? GetStoredResourceRequest(string id)
        {
            if (_resourceRequest.TryGetValue(id, out var resourceRequest))
            {
                if (resourceRequest.Creation < DateTime.UtcNow.AddMinutes(-1))
                {
                    _resourceRequest.TryRemove(id, out _);
                    return null;
                }
                return resourceRequest.Permissions;
            }
            return null;
        }


    }
}
