using Microsoft.EntityFrameworkCore;
using Mingle.DAL.Content;
using Mingle.DAL.Seeding;
using Mingle.Engine;

namespace Mingle.DAL.Tests;

[Collection(PostgresCollection.Name)]
public class DatabaseTests(PostgresFixture postgres)
{
    private static readonly StarterContent Content = StarterContent.Current();

    // Straight to the connection: card texts hold braces ("{@}"), which EF's raw SQL would read as placeholders.
    private static async Task Run(MingleDbContext db, string sql)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Migrations_create_every_table_and_the_card_kinds()
    {
        await using var db = await postgres.NewDatabaseAsync();

        var tables = await db.Database
            .SqlQueryRaw<string>("SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public'")
            .ToListAsync();

        Assert.Superset(
            new HashSet<string> { "card_kinds", "topics", "decks", "cards", "card_options", "card_topics", "deck_cards", "stories" },
            tables.ToHashSet());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(CardKinds.All.Select(k => k.Id).Order(), (await db.CardKinds.Select(k => k.Code).ToListAsync()).Order());
    }

    [Fact]
    public async Task Migrations_bring_all_the_built_in_content()
    {
        await using var db = await postgres.NewDatabaseAsync();
        var cards = Content.Decks.SelectMany(d => d.Cards).ToList();

        Assert.Equal(Content.Decks.Length, await db.Decks.CountAsync());
        Assert.Equal(cards.DistinctBy(c => c.Id).Count(), await db.Cards.CountAsync());
        Assert.Equal(cards.DistinctBy(c => c.Id).Sum(c => c.Texts.Values.Max(t => t.Options.Length)), await db.CardOptions.CountAsync());
        Assert.Equal(cards.DistinctBy(c => c.Id).Sum(c => c.Topics.Distinct().Count()), await db.CardTopics.CountAsync());
        Assert.Equal(cards.Count, await db.DeckCards.CountAsync());
        Assert.Equal(Content.Topics.Length, await db.Topics.CountAsync());
        Assert.Equal(Content.Stories.Length, await db.Stories.CountAsync());
    }

    [Fact]
    public async Task Content_migrations_run_again_on_a_database_that_has_the_content_change_nothing()
    {
        // A database seeded before content moved into migrations already has every row; the upserts must not fail
        // or duplicate anything.
        await using var db = await postgres.NewDatabaseAsync();
        var before = (await db.Cards.CountAsync(), await db.CardOptions.CountAsync(), await db.DeckCards.CountAsync());

        foreach (var migration in StarterContent.Migrations())
            foreach (var sql in ContentSql.Apply(migration.Content()))
                await Run(db, sql);

        Assert.Equal(before, (await db.Cards.CountAsync(), await db.CardOptions.CountAsync(), await db.DeckCards.CountAsync()));
    }

    [Fact]
    public async Task A_story_is_picked_in_the_asked_language_and_theme()
    {
        await using var db = await postgres.NewDatabaseAsync();
        var source = new DbStorySource(db);

        var story = await source.PickAsync(Lang.Hy, StoryTheme.Work, longStory: false, new HashSet<Guid>());

        Assert.NotNull(story);
        var row = await db.Stories.SingleAsync(s => s.Id == story.Id);
        Assert.Equal(Lang.Hy, row.Lang);
        Assert.Equal(StoryTheme.Work, row.Theme);
        // Stored as the enum's name, like the other enum columns.
        var langs = await db.Database.SqlQueryRaw<string>("SELECT DISTINCT lang AS \"Value\" FROM stories").ToListAsync();
        Assert.Equal(["En", "Hy", "Ru"], langs.Order());
    }

    [Fact]
    public async Task Decks_read_back_from_the_database_match_the_content_migrations()
    {
        await using var db = await postgres.NewDatabaseAsync();

        var stored = await new DbDeckSource(db).GetDecksAsync();

        foreach (var expected in Content.Decks)
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
                Assert.Equal(e.Texts.Keys.Order(), a.Texts.Keys.Order()); // some blitz cards exist in one language only
                foreach (var lang in e.Texts.Keys)
                {
                    Assert.Equal(e.Texts[lang].Text, a.Texts[lang].Text);
                    Assert.Equal(e.Texts[lang].Options.ToArray(), a.Texts[lang].Options.ToArray());
                }
            }
        }
    }

    [Fact]
    public async Task A_later_content_migration_updates_texts_moves_cards_and_keeps_ids()
    {
        await using var db = await postgres.NewDatabaseAsync();
        var optionIds = await db.CardOptions.OrderBy(o => o.Id).Select(o => o.Id).ToListAsync();
        var icebreakers = Content.Decks.Single(d => d.Id == "icebreakers");
        var first = icebreakers.Cards[0];

        // A fixed typo, and the deck loses its last card.
        var fixedCard = first with { Texts = first.Texts.SetItem(Lang.En, first.Texts[Lang.En] with { Text = "A fixed text" }) };
        var change = new ContentChange { Decks = [icebreakers with { Cards = [fixedCard, .. icebreakers.Cards[1..^1]] }] };
        foreach (var sql in ContentSql.Apply(change))
            await Run(db, sql);

        var card = await db.Cards.SingleAsync(c => c.Id == StableId.For("card", first.Id));
        Assert.Equal("A fixed text", card.Text.En);
        Assert.Equal(icebreakers.Cards.Length - 1, await db.DeckCards.CountAsync(l => l.DeckId == StableId.For("deck", "icebreakers")));
        Assert.Equal(optionIds, await db.CardOptions.OrderBy(o => o.Id).Select(o => o.Id).ToListAsync());

        // Removing a card takes it out of the decks and the database.
        var dropped = icebreakers.Cards[^1].Id;
        foreach (var sql in ContentSql.Apply(new ContentChange { RemovedCards = [first.Id] }))
            await Run(db, sql);
        Assert.False(await db.Cards.AnyAsync(c => c.Id == StableId.For("card", first.Id)));
        Assert.True(await db.Cards.AnyAsync(c => c.Id == StableId.For("card", dropped))); // left the deck, but not removed
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
