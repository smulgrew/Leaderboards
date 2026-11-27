using System.Text.Json.Serialization;
using LeadersBoards.Api.Data;
using LeadersBoards.Api.Domain;
using LeadersBoards.Api.Endpoints;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o => o
    .UseNpgsql(builder.Configuration.GetConnectionString("Default"))
    .UseSnakeCaseNamingConvention());

// Send/receive enums as strings ("Time", "RoundsReps") so the mobile app gets readable JSON.
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Prices and member limits live in appsettings.json "Plans" (see Domain/PlanOptions.cs).
builder.Services.Configure<PlanOptions>(builder.Configuration.GetSection("Plans"));

builder.Services.AddOpenApi();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // JSON spec at /openapi/v1.json
    app.MapDevAuth();

    // Apply migrations on startup in dev so "dotnet run" is all you need.
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.UseCors();
app.MapGet("/health", () => Results.Ok("ok"));
app.MapLeaderboards();
app.MapInvites();
app.MapEvents();

app.Run();
