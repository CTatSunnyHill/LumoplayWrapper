using IntTech_Controller_Backend.Data;
using IntTech_Controller_Backend.Services;
using Microsoft.EntityFrameworkCore;


var builder = WebApplication.CreateBuilder(args);

var mongoStr = builder.Configuration.GetConnectionString("MongoDb");
builder.Services.AddDbContext<IntTechDBContext>(options => options.UseMongoDB(mongoStr ?? "mongo://localhost:27017", "inttech_controller"));

// Add services to the container.

builder.Services.AddSingleton<LumoCommandService>();
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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();

app.Run("Http://0.0.0.0:5221");
