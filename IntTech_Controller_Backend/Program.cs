using IntTech_Controller_Backend.Data;
using IntTech_Controller_Backend.Services;
using Microsoft.EntityFrameworkCore;


var builder = WebApplication.CreateBuilder(args);

var mongoStr = builder.Configuration.GetConnectionString("MongoDb");
builder.Services.AddDbContext<IntTechDBContext>(options => options.UseMongoDB(mongoStr ?? "mongo://localhost:27017", "inttech_controller"));

// Add services to the container.

builder.Services.AddSingleton<LumoCommandService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
