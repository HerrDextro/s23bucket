using cloud.api.Models;
using cloud.api.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using cloud.api.Dtos;

namespace cloud.api.Endpoints; //file scoped is cleaner 

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", () =>
        {
            return Results.Ok("Hello from the API!");
        });

        app.MapPost("/auth/register",async (UserDto userDto, UserService userService) => 
        {
            var newUser = new User
            {
                Username = userDto.username,
                HashedPassword = BCrypt.Net.BCrypt.HashPassword(userDto.password)
            };
            await userService.RegisterUser(newUser);
            return Results.Created();
        });

        app.MapPost("/auth/login", async (UserDto userDto, UserService userService, JWTService jwtService) => 
        {
            var user = await userService.LoginUser(userDto);
            if (user == null)
            {
                return Results.Unauthorized();
            }
            else
            {
                var jwt = jwtService.GenerateToken(user);
                return Results.Ok(new { token = jwt });
            }
        });

    } 
}



