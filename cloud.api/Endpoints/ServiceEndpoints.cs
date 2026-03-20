using cloud.api.Dtos;
using cloud.api.Services;

namespace cloud.api.Endpoints
{
    public static class ServiceEndpoints
    {
        public static void MapServiceEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapPost("/service/register", async (ExternalClientDto serviceDto, ExternalClientService serviceService) => //NO AUTH FOR NOW
            {
                await serviceService.StoreService(serviceDto);
            });

            app.MapGet("/service/getall", async (ExternalClientService serviceService) => 
            { 
                var services = await serviceService.GetAllServices();
                return Results.Ok(services);
            }); 
        }
    }
}
