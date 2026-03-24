using cloud.api.Dtos;
using cloud.api.Services;
using Microsoft.AspNetCore.Mvc;

namespace cloud.api.Endpoints
{
    public static class ExternalClientEndpoints
    {
        public static void MapServiceEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapPost("/service/register", async (ExternalClientDto serviceDto, ExternalClientService serviceService) => //NO AUTH FOR NOW
            {
                var externalClient = await serviceService.GetServiceByName(serviceDto.name);
                if (externalClient != null)
                {
                    return Results.BadRequest("Service with this name already exists");
                }
                await serviceService.StoreService(serviceDto);  //creates identical services without restriction!!!
                return Results.Created();
            });

            app.MapGet("/service/getall", async (ExternalClientService serviceService) => 
            { 
                var services = await serviceService.GetAllServices();
                return Results.Ok(services);
            });

            app.MapPost("/service/request", async ([FromBody]ResourceRequestDto requestDto, [FromServices]JWTService jwtService, ExternalClientService externalClientService) => 
            {
                var requestId = externalClientService.StoreResourceRequest(requestDto.resource_id_permission); //Test this carefully. Tested: [ ]

                var tempToken = jwtService.GenerateClientToken(requestId);
                return Results.Ok(new { token = tempToken });

            });
        }
    }
}
