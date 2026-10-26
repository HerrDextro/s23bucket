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
var secretKeyText = jwtSettings["SecretKey"];
if (String.IsNullOrWhiteSpace(secretKeyText) || Encoding.UTF8.GetBytes(secretKeyText).Length < 32)
{
    throw new InvalidOperationException("JWT Secret Key is null, whitespace or < 32 characters. Please check your configuration.");
}
var secretKey = Encoding.UTF8.GetBytes(secretKeyText);

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

//jwt service for generating tokens
builder.Services.AddScoped<JWTService>();
//binding JwtSettings to JWTSettings class
builder.Services.Configure<JWTSettings>(jwtSettings); //dont have to GetSection because we already have it in the jwtSettings variable

//user service (mongodb for users)
builder.Services.AddScoped<UserService>(); //my service for the endpoints to use user db
//binding appsettings.json to class
builder.Services.Configure<MongoSettings>(builder.Configuration.GetSection("MongoDBSettings")); //bind class mongosettings to json in appsettings.json, so we can inject it in the controllers and use it to get the connection string and database name.

//auth service
builder.Services.AddScoped<AuthService>();
//cursed service service
builder.Services.AddSingleton<ExternalClientService>(); //singleton because list of resource requests needs to be kept in memory

//file service
builder.Services.AddScoped<FileService>();

//more endpoint ahh service
//builder.Services.AddHttpContextAccessor();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    //app.UseSwagger();
    //app.UseSwaggerUI(); // This is the actual webpage
}


//app.UseHttpsRedirection();
app.UseAuthentication(); 
app.UseAuthorization(); //there 2 must remain in this order

app.MapAuthEndpoints();
app.MapServiceEndpoints();
app.MapFileEndpoints();

app.Run();


//notes:
//implement cors
//implement anti forgery
//ask chatter if refreshtokens should be hashed, if so implement hashing and salting for refresh tokens in the auth service