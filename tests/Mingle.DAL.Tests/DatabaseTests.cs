using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Mingle.DAL.Seeding;
using Mingle.Engine;

namespace Mingle.DAL.Tests;

[Collection(PostgresCollection.Name)]
public class DatabaseTests(PostgresFixture postgres)
{
    private static DeckSeeder Seeder(MingleDbContext db) => new(db, NullLogger<DeckSeeder>.Instance);

    [Fact]
    public async Task Migrations_create_every_table()
    {
        await using var db = await postgres.NewDatabaseAsync();

        var tables = await db.Database
            .SqlQueryRaw<string>("SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public'")
            .ToListAsync();

        Assert.Superset(
            new HashSet<string> { "decks", "deck_names", "cards", "card_translations", "deck_cards" },
            tables.ToHashSet());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task Seeding_imports_the_starter_decks_once()
    {
        await using var db = await postgres.NewDatabaseAsync();

        await Seeder(db).SeedAsync();
        await Seeder(db).SeedAsync(); // a second start must not duplicate anything

        Assert.Equal(2, await db.Decks.CountAsync());
        Assert.Equal(40, await db.Cards.CountAsync());
        Assert.Equal(120, await db.CardTranslations.CountAsync());
        Assert.Equal(40, await db.DeckCards.CountAsync());
    }

    [Fact]
    public async Task Decks_read_back_from_the_database_match_the_seed_files()
    {
        await using var db = await postgres.NewDatabaseAsync();
        await Seeder(db).SeedAsync();
        db.ChangeTracker.Clear();

        var stored = await db.Decks
            .Include(d => d.Names)
            .Include(d => d.Cards).ThenInclude(c => c.Card).ThenInclude(c => c.Translations)
            .AsSplitQuery()
            .ToListAsync();

        foreach (var file in Directory.GetFiles(DeckSeeder.DefaultFolder, "*.json"))
        {
            var expected = DeckJson.Parse(await File.ReadAllTextAsync(file));
            var actual = DeckMapping.ToGame(stored.Single(d => d.Id == expected.Id));
            Assert.Equal(DeckJson.WriteMany([expected]), DeckJson.WriteMany([actual]));
        }
    }

    [Fact]
    public async Task The_database_rejects_cards_with_an_invalid_depth()
    {
        await using var db = await postgres.NewDatabaseAsync();
        db.Cards.Add(new Entities.Card { Id = "too-deep", KindId = "open", Depth = 4 });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
