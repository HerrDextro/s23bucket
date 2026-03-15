using cloud.api.Endpoints;
using cloud.api.Services;
using cloud.api.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration["MongoDBSettings:ConnectionString"];

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]);



// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(secretKey),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero // Removes the 5-minute "grace period" for expiry, add if possibility of server drift exists
        };
    });

builder.Services.AddAuthorization();

// 2. Register the CLIENT as a Singleton (The "Engine")
builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(connectionString));

// 3. Register the DATABASE as Scoped (The "Office")
builder.Services.AddScoped(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    return client.GetDatabase("maindb");
});
//user service (mongodb for users)
builder.Services.AddScoped<UserService>(); //my service for the endpoints to use user db
//binding appsettings.json to class
builder.Services.Configure<MongoSettings>(builder.Configuration.GetSection("MongoDBSettings")); //bind class mongosettings to json in appsettings.json, so we can inject it in the controllers and use it to get the connection string and database name.

//jwt service for generating tokens
builder.Services.AddScoped<JWTService>();
//binding JwtSettings to JWTSettings class
builder.Services.Configure<JWTSettings>(jwtSettings); //dont have to GetSection because we already have it in the jwtSettings variable
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    //app.UseSwagger();
    //app.UseSwaggerUI(); // This is the actual webpage
}


//app.UseHttpsRedirection();

app.UseAuthorization();
app.UseAuthentication();

app.MapAuthEndpoints();

app.Run();
