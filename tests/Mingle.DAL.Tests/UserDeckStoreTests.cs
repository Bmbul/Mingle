using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Mingle.DAL.Entities;
using Mingle.DAL.Seeding;
using Mingle.Engine;
using GameCard = Mingle.Engine.Card;
using GameDeck = Mingle.Engine.Deck;

namespace Mingle.DAL.Tests;

[Collection(PostgresCollection.Name)]
public class UserDeckStoreTests(PostgresFixture postgres)
{
    private static GameCard Open(string id, string en, string? hy = null)
    {
        var texts = ImmutableDictionary<Lang, CardText>.Empty.Add(Lang.En, new CardText(en));
        if (hy is not null) texts = texts.Add(Lang.Hy, new CardText(hy));
        return new GameCard(id, "open", 1, [], texts);
    }

    private static GameCard Choice(string id, string a, string b) =>
        new(id, "wyr", 1, [], ImmutableDictionary<Lang, CardText>.Empty.Add(Lang.En, new CardText("Would you rather…", [a, b])));

    private static GameDeck Deck(string name, params GameCard[] cards) =>
        new("new", ImmutableDictionary<Lang, string>.Empty.Add(Lang.Hy, name), [.. cards]);

    private async Task<MingleDbContext> SeededAsync()
    {
        var db = await postgres.NewDatabaseAsync();
        await new DeckSeeder(db, NullLogger<DeckSeeder>.Instance).SeedAsync();
        db.ChangeTracker.Clear();
        return db;
    }

    [Fact]
    public async Task A_new_deck_can_be_read_by_id_but_is_not_a_starter_deck()
    {
        await using var db = await SeededAsync();
        var store = new UserDeckStore(db);

        var (id, key) = await store.CreateAsync(Deck("Մերը", Open("x", "First?", "Առաջինը՞"), Choice("y", "Tea", "Coffee")));
        db.ChangeTracker.Clear();

        var deck = (await store.GetAsync(id))!;
        Assert.Equal("Մերը", deck.NameFor(Lang.Ru)); // falls back to the only name it has
        Assert.Equal(["First?", "Would you rather…"], deck.Cards.Select(c => c.Texts[Lang.En].Text).ToArray());
        Assert.Equal(["Tea", "Coffee"], deck.Cards[1].Texts[Lang.En].Options.ToArray());
        Assert.True(key.Length >= 40);
        Assert.DoesNotContain(await new DbDeckSource(db).GetDecksAsync(), d => d.Id == id.ToString());
        Assert.Equal(CardSource.User, (await db.Cards.SingleAsync(c => c.Id == Guid.Parse(deck.Cards[0].Id))).Source);
    }

    [Fact]
    public async Task Only_the_edit_key_can_change_or_delete_a_deck()
    {
        await using var db = await SeededAsync();
        var store = new UserDeckStore(db);
        var (id, key) = await store.CreateAsync(Deck("Mine", Open("x", "First?")));

        Assert.Equal(DeckChange.WrongKey, await store.SaveAsync(id, "guess", Deck("Hacked")));
        Assert.Equal(DeckChange.WrongKey, await store.SaveAsync(id, null, Deck("Hacked")));
        Assert.Equal(DeckChange.WrongKey, await store.DeleteAsync(id, "guess"));
        Assert.Equal(DeckChange.NotFound, await store.SaveAsync(Guid.NewGuid(), key, Deck("Other")));

        var starter = (await new DbDeckSource(db).GetDecksAsync())[0];
        Assert.Equal(DeckChange.WrongKey, await store.SaveAsync(Guid.Parse(starter.Id), key, Deck("Hacked")));
    }

    [Fact]
    public async Task Saving_edits_own_cards_in_place_and_forks_changed_starter_cards()
    {
        await using var db = await SeededAsync();
        var store = new UserDeckStore(db);
        var starter = (await new DbDeckSource(db).GetDecksAsync())[0];
        var kept = starter.Cards[0];
        var changed = starter.Cards[1];

        var (id, key) = await store.CreateAsync(Deck("Mine", Open("x", "First?"), Open("gone", "Remove me")));
        db.ChangeTracker.Clear();
        var own = (await store.GetAsync(id))!.Cards[0];

        var edited = changed with { Texts = changed.Texts.SetItem(Lang.En, new CardText("My own wording?")) };
        Assert.Equal(DeckChange.Done, await store.SaveAsync(id, key,
            Deck("Mine", own with { Texts = own.Texts.SetItem(Lang.En, new CardText("First, edited?")) }, kept, edited)));
        db.ChangeTracker.Clear();

        var deck = (await store.GetAsync(id))!;
        Assert.Equal(own.Id, deck.Cards[0].Id);                 // edited in place
        Assert.Equal("First, edited?", deck.Cards[0].Texts[Lang.En].Text);
        Assert.Equal(kept.Id, deck.Cards[1].Id);                // starter card linked as it is
        Assert.NotEqual(changed.Id, deck.Cards[2].Id);          // a changed starter card becomes a copy
        Assert.Equal(changed.Topics.ToArray(), deck.Cards[2].Topics.ToArray());

        var copy = await db.Cards.SingleAsync(c => c.Id == Guid.Parse(deck.Cards[2].Id));
        Assert.Equal(Guid.Parse(changed.Id), copy.ForkedFromId);
        var original = await db.Cards.SingleAsync(c => c.Id == Guid.Parse(changed.Id));
        Assert.Equal(changed.Texts[Lang.En].Text, original.Text.En); // starter card untouched
        Assert.Equal(2, await db.Cards.CountAsync(c => c.Source == CardSource.User)); // "gone" was removed
    }

    [Fact]
    public async Task Deleting_a_deck_removes_its_own_cards_but_not_starter_cards()
    {
        await using var db = await SeededAsync();
        var store = new UserDeckStore(db);
        var starterCard = (await new DbDeckSource(db).GetDecksAsync())[0].Cards[0];
        var cardsBefore = await db.Cards.CountAsync();

        var (id, key) = await store.CreateAsync(Deck("Mine", Open("x", "First?"), starterCard));
        db.ChangeTracker.Clear();
        Assert.Equal(DeckChange.Done, await store.DeleteAsync(id, key));
        db.ChangeTracker.Clear();

        Assert.Null(await store.GetAsync(id));
        Assert.Equal(cardsBefore, await db.Cards.CountAsync());
    }
}
