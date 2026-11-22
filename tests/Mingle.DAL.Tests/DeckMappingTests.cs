using System.Collections.Immutable;
using Mingle.DAL;
using Mingle.Engine;

namespace Mingle.DAL.Tests;

public class DeckMappingTests
{
    private static Deck SeedDeck(string file) =>
        DeckJson.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Seed", file)));

    [Theory]
    [InlineData("icebreakers.json")]
    [InlineData("couples.json")]
    public void Seed_decks_survive_the_trip_through_database_entities(string file)
    {
        var deck = SeedDeck(file);

        var row = DeckMapping.ToEntity(deck, new Dictionary<string, DAL.Entities.Card>());
        var back = DeckMapping.ToGame(row);

        Assert.Equal(DeckJson.WriteMany([deck]), DeckJson.WriteMany([back]));
    }

    [Fact]
    public void Card_order_comes_from_position()
    {
        var deck = SeedDeck("icebreakers.json");
        var row = DeckMapping.ToEntity(deck, new Dictionary<string, DAL.Entities.Card>());
        row.Cards.Reverse(); // the database returns rows in any order

        Assert.Equal(deck.Cards.Select(c => c.Id), DeckMapping.ToGame(row).Cards.Select(c => c.Id));
    }

    [Fact]
    public void A_card_in_two_decks_is_stored_once()
    {
        var shared = DeckMapping.ToGame(DeckMapping.ToEntity(SeedDeck("couples.json").Cards[0]));
        var names = ImmutableDictionary<Lang, string>.Empty.Add(Lang.En, "x");
        var known = new Dictionary<string, DAL.Entities.Card>();

        var first = DeckMapping.ToEntity(new Deck("a", names, [shared]), known);
        var second = DeckMapping.ToEntity(new Deck("b", names, [shared]), known);

        Assert.Same(first.Cards[0].Card, second.Cards[0].Card);
        Assert.Single(known);
    }
}
