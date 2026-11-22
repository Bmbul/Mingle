using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Mingle.DAL.Seeding;
using Mingle.Engine;

namespace Mingle.DAL.Tests;

[Collection(PostgresCollection.Name)]
public class DatabaseTests(PostgresFixture postgres)
{
    private static DeckSeeder Seeder(MingleDbContext db) => new(db, NullLogger<DeckSeeder>.Instance);

    private static IEnumerable<Deck> SeedDecks(string folder) =>
        Directory.GetFiles(Path.Combine(folder, "Decks"), "*.json").Select(f => DeckJson.Parse(File.ReadAllText(f)));

    [Fact]
    public async Task Migrations_create_every_table_and_the_card_kinds()
    {
        await using var db = await postgres.NewDatabaseAsync();

        var tables = await db.Database
            .SqlQueryRaw<string>("SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public'")
            .ToListAsync();

        Assert.Superset(
            new HashSet<string> { "card_kinds", "topics", "decks", "cards", "card_options", "card_topics", "deck_cards" },
            tables.ToHashSet());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(CardKinds.All.Select(k => k.Id).Order(), (await db.CardKinds.Select(k => k.Code).ToListAsync()).Order());
    }

    [Fact]
    public async Task Seeding_imports_everything_once()
    {
        await using var db = await postgres.NewDatabaseAsync();
        var decks = SeedDecks(DeckSeeder.DefaultFolder).ToList();
        var cards = decks.SelectMany(d => d.Cards).ToList();

        await Seeder(db).SeedAsync();
        await Seeder(db).SeedAsync(); // a second start must not duplicate anything

        Assert.Equal(decks.Count, await db.Decks.CountAsync());
        Assert.Equal(cards.Count, await db.Cards.CountAsync());
        Assert.Equal(cards.Sum(c => c.Texts[Lang.En].Options.Length), await db.CardOptions.CountAsync());
        Assert.Equal(cards.Sum(c => c.Topics.Length), await db.CardTopics.CountAsync());
        Assert.Equal(cards.Count, await db.DeckCards.CountAsync());
        var topicCount = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(DeckSeeder.DefaultFolder, "topics.json"))).RootElement.GetArrayLength();
        Assert.Equal(topicCount, await db.Topics.CountAsync());
    }

    [Fact]
    public async Task Decks_read_back_from_the_database_match_the_seed_files()
    {
        await using var db = await postgres.NewDatabaseAsync();
        await Seeder(db).SeedAsync();
        db.ChangeTracker.Clear();

        var stored = await new DbDeckSource(db).GetDecksAsync();

        foreach (var expected in SeedDecks(DeckSeeder.DefaultFolder))
        {
            var actual = stored.Single(d => d.Id == StableId.For("deck", expected.Id).ToString());
            Assert.Equal(expected.Names.OrderBy(n => n.Key), actual.Names.OrderBy(n => n.Key));
            Assert.Equal(expected.Cards.Length, actual.Cards.Length);
            for (var i = 0; i < expected.Cards.Length; i++)
            {
                var (e, a) = (expected.Cards[i], actual.Cards[i]);
                Assert.Equal(StableId.For("card", e.Id).ToString(), a.Id);
                Assert.Equal((e.KindId, e.Depth), (a.KindId, a.Depth));
                Assert.Equal(e.Topics.Order(StringComparer.Ordinal), a.Topics);
                foreach (var lang in Langs.All)
                {
                    Assert.Equal(e.Texts[lang].Text, a.Texts[lang].Text);
                    Assert.Equal(e.Texts[lang].Options.ToArray(), a.Texts[lang].Options.ToArray());
                }
            }
        }
    }

    [Fact]
    public async Task Seeding_again_updates_changed_texts_and_keeps_ids()
    {
        await using var db = await postgres.NewDatabaseAsync();
        await Seeder(db).SeedAsync();
        var optionIds = await db.CardOptions.OrderBy(o => o.Id).Select(o => o.Id).ToListAsync();

        // A copy of the seed folder with one fixed typo.
        var folder = Directory.CreateTempSubdirectory("mingle-seed-").FullName;
        File.Copy(Path.Combine(DeckSeeder.DefaultFolder, "topics.json"), Path.Combine(folder, "topics.json"));
        Directory.CreateDirectory(Path.Combine(folder, "Decks"));
        foreach (var file in Directory.GetFiles(Path.Combine(DeckSeeder.DefaultFolder, "Decks")))
        {
            var json = File.ReadAllText(file).Replace("What's a small thing that always makes your day better?", "What small thing always makes your day better?");
            File.WriteAllText(Path.Combine(folder, "Decks", Path.GetFileName(file)), json);
        }

        db.ChangeTracker.Clear();
        await Seeder(db).SeedAsync(folder);
        db.ChangeTracker.Clear();

        var card = await db.Cards.SingleAsync(c => c.Id == StableId.For("card", "ice-01"));
        Assert.Equal("What small thing always makes your day better?", card.Text.En);
        Assert.Equal(SeedDecks(DeckSeeder.DefaultFolder).Sum(d => d.Cards.Count()), await db.Cards.CountAsync());
        Assert.Equal(optionIds, await db.CardOptions.OrderBy(o => o.Id).Select(o => o.Id).ToListAsync());
    }

    [Fact]
    public async Task The_database_rejects_cards_with_an_invalid_depth_or_kind()
    {
        await using var db = await postgres.NewDatabaseAsync();

        db.Cards.Add(new Entities.Card { KindId = 1, Depth = 4 });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        db.ChangeTracker.Clear();
        db.Cards.Add(new Entities.Card { KindId = 99, Depth = 1 });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
