using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mingle.Engine;

namespace Mingle.DAL.Seeding;

/// <summary>Loads the starter decks from <c>Seed/*.json</c> into an empty database.</summary>
public sealed class DeckSeeder(MingleDbContext db, ILogger<DeckSeeder> log)
{
    public static string DefaultFolder => Path.Combine(AppContext.BaseDirectory, "Seed");

    public async Task SeedAsync(string? seedFolder = null, CancellationToken ct = default)
    {
        if (await db.Decks.AnyAsync(ct))
        {
            log.LogInformation("Decks already present; skipping seed.");
            return;
        }

        var knownCards = new Dictionary<string, Entities.Card>();
        foreach (var file in Directory.GetFiles(seedFolder ?? DefaultFolder, "*.json").Order())
        {
            var deck = DeckJson.Parse(await File.ReadAllTextAsync(file, ct));
            db.Decks.Add(DeckMapping.ToEntity(deck, knownCards));
            log.LogInformation("Seeding deck {Deck} with {Count} cards.", deck.Id, deck.Cards.Length);
        }

        await db.SaveChangesAsync(ct);
    }
}
