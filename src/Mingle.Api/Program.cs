using Microsoft.EntityFrameworkCore;
using Mingle.Api.Hubs;
using Mingle.Api.Rooms;
using Mingle.Contracts;
using Mingle.DAL;
using Mingle.DAL.Seeding;
using Mingle.Engine;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMingleDal(builder.Configuration.GetConnectionString("Mingle")
    ?? throw new InvalidOperationException("Missing connection string 'Mingle'."));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<RoomRegistry>();
builder.Services.AddHostedService<RoomCleanupService>();
builder.Services.AddSignalR()
    .AddJsonProtocol(o => ContractsJsonContext.Register(o.PayloadSerializerOptions));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}

// Bring the schema up to date and load the starter decks into an empty database.
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<MingleDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<DeckSeeder>().SeedAsync();
}

// Serves the Blazor WebAssembly client (framework files included) with fingerprinting and compression.
app.MapStaticAssets();

app.MapGet("/api/decks", async (IDeckSource decks, CancellationToken ct) =>
    Results.Text(DeckJson.WriteMany(await decks.GetDecksAsync(ct)), "application/json; charset=utf-8"));

app.MapHub<GameHub>(HubRoutes.Game);

app.MapGet("/health", () => Results.Ok("ok"));

app.MapFallbackToFile("index.html");

app.Run();

// Lets the integration tests start the app with WebApplicationFactory<Program>.
public partial class Program;
