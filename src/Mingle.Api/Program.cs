using Microsoft.EntityFrameworkCore;
using Mingle.DAL;
using Mingle.DAL.Seeding;
using Mingle.Engine;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMingleDal(builder.Configuration.GetConnectionString("Mingle")
    ?? throw new InvalidOperationException("Missing connection string 'Mingle'."));

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

app.MapGet("/api/decks", async (MingleDbContext db, CancellationToken ct) =>
{
    var decks = await db.Decks
        .AsNoTracking()
        .Include(d => d.Names)
        .Include(d => d.Cards).ThenInclude(c => c.Card).ThenInclude(c => c.Translations)
        .OrderBy(d => d.CreatedAt).ThenBy(d => d.Id)
        .AsSplitQuery()
        .ToListAsync(ct);

    return Results.Text(DeckJson.WriteMany(decks.Select(DeckMapping.ToGame)), "application/json; charset=utf-8");
});

app.MapGet("/health", () => Results.Ok("ok"));

app.MapFallbackToFile("index.html");

app.Run();
