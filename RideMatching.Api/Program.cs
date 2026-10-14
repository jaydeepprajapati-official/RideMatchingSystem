using Microsoft.EntityFrameworkCore;
using RideMatching.Api.BackgroundJobs;
using RideMatching.Api.Data;
using RideMatching.Api.Hubs;
using RideMatching.Api.Middleware;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<RideDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSignalR();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();


builder.Services.AddCors(options =>
{
    options.AddPolicy("RideClient", policy =>
    {
        policy
            .WithOrigins("http://localhost:3000", "https://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddHostedService<RideMatchingWorker>();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Maps the interactive Scalar UI
    app.MapScalarApiReference(); //
}

app.UseHttpsRedirection();
app.UseCors("RideClient");

app.UseAuthorization();

app.MapControllers();

app.MapHub<RideHub>("/hubs/rides");

app.Run();
