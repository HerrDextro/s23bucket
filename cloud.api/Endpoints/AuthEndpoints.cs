using cloud.api.Models;
using cloud.api.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using cloud.api.Dtos;
using System.Security.Claims;

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

        app.MapPost("/auth/login", async (UserDto userDto, UserService userService, JWTService jwtService, AuthService authService) => 
        {
            var user = await userService.LoginUser(userDto);
            if (user == null)
            {
                return Results.Unauthorized();
            }
            else
            {
                var jwt = jwtService.GenerateToken(user);
                var refreshToken = await authService.GenerateRefreshToken(user);
                authService.StoreRefreshToken(refreshToken);
                return Results.Ok(new { token = jwt, refreshToken = refreshToken.Token});
            }
        });

        app.MapPost("/auth/refresh", async (RefreshDto dto, AuthService authService, JWTService jwtService, UserService userService) => 
        {
            var token = dto.RefreshToken;
            var storedRefreshToken = await authService.GetRefreshTokenByToken(token);
            if(storedRefreshToken == null || storedRefreshToken.ExpiryDate < DateTime.UtcNow || storedRefreshToken.IsRevoked)
            {
                return Results.Unauthorized();
            }

            var userId = storedRefreshToken.UserId;
            var userObj =  await userService.GetUserById(userId);
            var jwt = jwtService.GenerateToken(userObj);
            return Results.Ok(new { token = jwt }); //fixed window, forced re login
        });



    } 
}



