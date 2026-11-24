using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Anthropic;
using Mingle.API.Controllers;
using Mingle.API.Ai;
using Mingle.API.Hubs;
using Mingle.API.Rooms;
using Mingle.Contracts;
using Mingle.DAL;
using Mingle.DAL.Seeding;
using Mingle.Engine;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMingleDal(builder.Configuration.GetConnectionString("Mingle")
    ?? throw new InvalidOperationException("Missing connection string 'Mingle'."));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<RoomRegistry>();
builder.Services.AddControllers();
builder.Services.AddRateLimiter(DecksController.AddWriteLimit);

// AI card writing for the "Our topics" game. Without a key, games use library cards instead.
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection(AiOptions.Section));
builder.Services.AddSingleton<DailyLimit>();
var aiKey = builder.Configuration[$"{AiOptions.Section}:ApiKey"] ?? builder.Configuration["ANTHROPIC_API_KEY"];
if (!string.IsNullOrWhiteSpace(aiKey))
{
    builder.Services.AddSingleton(new AnthropicClient { ApiKey = aiKey });
    builder.Services.AddSingleton<ICardWriter, ClaudeCardWriter>();
    builder.Services.AddSingleton<IStoryWriter, ClaudeStoryWriter>();
}
else
{
    builder.Services.AddSingleton<ICardWriter, NoCardWriter>();
    builder.Services.AddSingleton<IStoryWriter, NoStoryWriter>();
}
builder.Services.AddHostedService<RoomCleanupService>();
// Behind Azure Container Apps the app sees plain HTTP; the proxy's headers say the visitor used HTTPS.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});
builder.Services.AddSignalR()
    .AddJsonProtocol(o => ContractsJsonContext.Register(o.PayloadSerializerOptions));

var app = builder.Build();

app.UseForwardedHeaders();
app.UseRateLimiter();

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

app.MapControllers();

app.MapHub<GameHub>(HubRoutes.Game);

app.MapGet("/health", () => Results.Ok("ok"));

app.MapFallbackToFile("index.html");

app.Run();

// Lets the integration tests start the app with WebApplicationFactory<Program>.
public partial class Program;
