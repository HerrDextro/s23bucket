using cloud.api.Dtos;
using cloud.api.Services;

namespace cloud.api.Endpoints
{
    public static class ServiceEndpoints
    {
        public static void MapServiceEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapPost("/service/register", async (ServiceDto serviceDto, CursedServiceService serviceService) => //NO AUTH FOR NOW
            { 
                serviceService.StoreService(new Models.Service 
                { 
                    Name = serviceDto.name, 
                    URL = serviceDto.url, 
                    ApiKey = serviceDto.api_key 
                });
            });

            app.MapGet("/service/getall", async (CursedServiceService serviceService) => 
            { 
                var services = await serviceService.GetAllServices();
            });
        }
    }
}
