using IntTech_Controller_Backend.Data;
using IntTech_Controller_Backend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using IntTech_Controller_Backend.Models;
using MongoDB.Bson;


var builder = WebApplication.CreateBuilder(args);

var mongoStr = builder.Configuration.GetConnectionString("MongoDb");
builder.Services.AddDbContext<IntTechDBContext>(options => options.UseMongoDB(mongoStr ?? "mongo://localhost:27017", "inttech_controller"));

// --- JWT Authentication Setup --- 

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

builder.Services.AddSingleton<LumoCommandService>();
builder.Services.AddSingleton<ProjectorCommandService>();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
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
            AllowedLocationsIds = new List<ObjectId> { }
        });
        db.SaveChanges();
        Console.WriteLine("Seeded default admin user: admin / admin");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run("Http://0.0.0.0:5221");
