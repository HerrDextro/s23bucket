# ASP.NET + MongoDB Schnellstart (C#)

## 1. Setup

dotnet new web -n MyApi  
cd MyApi  
dotnet add package MongoDB.Driver

----------

# ASP.NET Core Grundlagen

## appsettings.json

{  
 "MySettings": {  
 "Message": "Hello World"  
 },  
 "MongoDb": {  
 "ConnectionString": "mongodb://localhost:27017",  
 "DatabaseName": "mydb",  
 "CollectionName": "items"  
 }  
}

----------

## Settings-Klasse

public  class  MySettings  
{  
  public  string  Message { get; set; } =  "";  
}

----------

## Settings registrieren (Program.cs)

builder.Services.Configure<MySettings>(  
  builder.Configuration.GetSection("MySettings"));

----------

## Settings verwenden (DI)

using  Microsoft.Extensions.Options;  
  
public  class  MyService  
{  
  private  readonly  MySettings  _settings;  
  
  public  MyService(IOptions<MySettings>  options)  
 {  
  _settings  =  options.Value;  
 }  
  
  public  string  GetMessage() =>  _settings.Message;  
}

----------

## Service registrieren

builder.Services.AddScoped<MyService>();

----------

## Minimal API Endpoint

app.MapGet("/message", (MyService  service) =>  
{  
  return  service.GetMessage();  
});

----------

## Endpoints in Dateien aufteilen

### Extension-Klasse erstellen

public  static  class  MyEndpoints  
{  
  public  static  void  MapMyEndpoints(this  IEndpointRouteBuilder  app)  
 {  
  app.MapGet("/hello", () =>  "Hello");  
 }  
}

### In Program.cs verwenden

app.MapMyEndpoints();

----------

## Parameter Binding

ASP.NET bindet automatisch:

-   Route: `/items/{id}`
-   Query: `/items?id=1`
-   Body (POST/PUT)
-   Services (via DI)

Beispiel:

app.MapPost("/echo", (MyModel  model) =>  model);

----------

## ⚠️ Achtung: "Failed to infer parameter"

Dieser Fehler tritt auf, wenn ASP.NET nicht erkennt, woher ein Parameter kommt.

### Lösung:

-   Verwende `[FromBody]`, `[FromServices]`, `[FromQuery]`

app.MapPost("/test",  
 ([FromBody] MyModel  model,  
 [FromServices] MyService  service) =>  
{  
  return  model;  
});

----------

# MongoDB Integration

## Mongo Settings-Klasse

public  class  MongoSettings  
{  
  public  string  ConnectionString { get; set; } =  "";  
  public  string  DatabaseName { get; set; } =  "";  
  public  string  CollectionName { get; set; } =  "";  
}

----------

## Mongo Settings registrieren

builder.Services.Configure<MongoSettings>(  
  builder.Configuration.GetSection("MongoDb"));

----------

## Mongo Service

using  MongoDB.Driver;  
using  Microsoft.Extensions.Options;  
  
public  class  MongoService  
{  
  private  readonly  IMongoCollection<Item>  _collection;  
  
  public  MongoService(IOptions<MongoSettings>  settings)  
 {  
  var  client  =  new  MongoClient(settings.Value.ConnectionString);  
  var  database  =  client.GetDatabase(settings.Value.DatabaseName);  
  
  _collection  =  database.GetCollection<Item>(  
  settings.Value.CollectionName);  
 }  
  
  public  async  Task<List<Item>>  GetAllAsync() =>  
  await  _collection.Find(_  =>  true).ToListAsync();  
  
  public  async  Task  InsertAsync(Item  item) =>  
  await  _collection.InsertOneAsync(item);  
}

----------

## Mongo Service registrieren

builder.Services.AddSingleton<MongoService>();

----------

## Datenmodell

using  MongoDB.Bson;  
using  MongoDB.Bson.Serialization.Attributes;  
  
public  class  Item  
{  
 [BsonId]  
 [BsonRepresentation(BsonType.ObjectId)]  
  public  string  Id { get; set; } =  "";  
  
  public  string  Name { get; set; } =  "";  
}

----------

## Endpoints mit Mongo

app.MapGet("/items", async (MongoService  db) =>  
{  
  return  await  db.GetAllAsync();  
});  
  
app.MapPost("/items", async (Item  item, MongoService  db) =>  
{  
  await  db.InsertAsync(item);  
  return  Results.Ok(item);  
});

----------

# Zusammenfassung

-   Verwende `IOptions<T>` für Konfiguration
-   Registriere Services mit Dependency Injection
-   Nutze Extension-Methoden zur Strukturierung
-   Behebe Binding-Fehler mit expliziten Attributen
-   MongoDB: `MongoClient → Database → Collection`

----------

Wenn es nicht funktioniert, dann ist es meistens:

-   falscher Connection String
-   MongoDB läuft nicht
-   oder DI wurde irgendwie kaputt konfiguriert
