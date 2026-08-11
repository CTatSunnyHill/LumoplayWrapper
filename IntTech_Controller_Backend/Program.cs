// Composition root for the controller backend: wires up MongoDB, JWT auth, and
// the HTTP pipeline, then performs first-run seeding before serving requests.

using IntTech_Controller_Backend.Data;
using IntTech_Controller_Backend.Helpers;
using IntTech_Controller_Backend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using IntTech_Controller_Backend.Models;
using MongoDB.Bson;
using MongoDB.Driver;


var builder = WebApplication.CreateBuilder(args);

var mongoStr = builder.Configuration.GetConnectionString("MongoDb");
builder.Services.AddDbContext<IntTechDBContext>(options => options.UseMongoDB(mongoStr ?? "mongo://localhost:27017", "inttech_controller"));

// --- JWT Authentication Setup ---

// Issuer and audience go unchecked: the backend both issues and consumes these
// tokens on a closed network, so the signature and expiry are what matter.
var jwt = builder.Configuration["Jwt:Key"] ?? "SuperSecretKeyForIntTechHospitalAppThatIsLongEnough";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => {
    options.TokenValidationParameters = new TokenValidationParameters {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt))
    };
});

// Add services to the container.

// Command services are singletons: both are stateless and hold process- or
// socket-level concurrency guards that must be shared across requests.
builder.Services.AddSingleton<LumoCommandService>();
builder.Services.AddSingleton<ProjectorCommandService>();
builder.Services.AddScoped<GameFileStorage>();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Serialise every ObjectId as a plain hex string.
        options.JsonSerializerOptions.Converters.Add(new ObjectIdConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()   // Allows IP 192.168.x.x
              .AllowAnyMethod()   // Allows GET, POST, DELETE, etc.
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// --- SEED Initial Admin User --
// Only runs on a virgin database, so an operator can sign in for the first time.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IntTechDBContext>();
    if (!db.Users.Any())
    {
        db.Users.Add(new User
        {
            Id = MongoDB.Bson.ObjectId.GenerateNewId(),
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin"), // Default password, should be changed after first login
            Role = "Admin",
            AllowedLocationsIds = new List<ObjectId> { },
            AllowedTagIds = new List<ObjectId> { }
        });
        db.SaveChanges();
        Console.WriteLine("Seeded default admin user: admin / admin");
    }
}

// --- Playlist compound unique index: (ownerId, name) per-user uniqueness ---
// Created through the raw driver because the EF Core Mongo provider cannot
// declare indexes. Failure is logged and tolerated: an index that already
// exists, or a database that is momentarily unreachable, must not stop startup.
{
    var mongoClient = new MongoClient(mongoStr ?? "mongodb://localhost:27017");
    var database = mongoClient.GetDatabase("inttech_controller");
    var playlistsCollection = database.GetCollection<BsonDocument>("playlists");
    var keys = Builders<BsonDocument>.IndexKeys.Ascending("ownerId").Ascending("name");
    var indexModel = new CreateIndexModel<BsonDocument>(
        keys,
        new CreateIndexOptions { Unique = true, Name = "owner_name_unique" });
    try
    {
        playlistsCollection.Indexes.CreateOne(indexModel);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Warning: Could not create playlist compound index: {ex.Message}");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

// Serves game artwork and one-pagers written by GameFileStorage.
app.UseStaticFiles();

app.UseAuthentication();
// Must sit between authentication and authorization: it needs the parsed
// principal, and it invalidates stale sessions before any policy is applied.
app.UseMiddleware<SessionVersionMiddleware>();
app.UseAuthorization();

app.MapControllers();

// Bound to all interfaces so devices and tablets on the ward network can reach it.
app.Run("Http://0.0.0.0:5221");
