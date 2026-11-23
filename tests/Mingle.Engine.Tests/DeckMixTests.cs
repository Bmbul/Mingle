using System.Collections.Immutable;
using static Mingle.Engine.Tests.TestData;

namespace Mingle.Engine.Tests;

public class DeckMixTests
{
    private static Deck Named(string id, int cards, params Lang[] langs) =>
        Deck([.. Enumerable.Range(1, cards).Select(i => Card($"{id}-{i:00}", CardKinds.FinishSentence, 1, langs))]) with { Id = id };

    private static readonly Lang[] Everyone = [Lang.En, Lang.Hy, Lang.Ru];

    [Fact]
    public void Takes_ten_cards_from_each_players_deck()
    {
        var mix = DeckMix.FromPicks([Named("a", 30), Named("b", 30)], Everyone, seed: 1);

        Assert.Equal(20, mix.Cards.Length);
        Assert.Equal(10, mix.Cards.Count(c => c.Id.StartsWith("a-")));
        Assert.Equal(10, mix.Cards.Count(c => c.Id.StartsWith("b-")));
    }

    [Fact]
    public void A_deck_brought_twice_gives_twice_as_many_cards_without_repeats()
    {
        var a = Named("a", 30);
        var mix = DeckMix.FromPicks([a, a, Named("b", 30)], Everyone, seed: 1);

        Assert.Equal(20, mix.Cards.Count(c => c.Id.StartsWith("a-")));
        Assert.Equal(mix.Cards.Length, mix.Cards.Select(c => c.Id).Distinct().Count());
    }

    [Fact]
    public void A_small_deck_gives_all_it_has()
    {
        var mix = DeckMix.FromPicks([Named("a", 4), Named("b", 30)], Everyone, seed: 1);

        Assert.Equal(14, mix.Cards.Length);
    }

    [Fact]
    public void Skips_cards_someone_at_the_table_cannot_read()
    {
        var englishOnly = Named("en", 10, Lang.En);
        var mix = DeckMix.FromPicks([englishOnly, Named("b", 10)], [Lang.En, Lang.Hy], seed: 1);

        Assert.All(mix.Cards, c => Assert.StartsWith("b-", c.Id));
    }

    [Fact]
    public void Same_seed_same_cards_other_seed_other_cards()
    {
        Deck[] picks = [Named("a", 40), Named("b", 40)];

        var first = DeckMix.FromPicks(picks, Everyone, seed: 7).Cards.Select(c => c.Id).ToArray();
        var again = DeckMix.FromPicks(picks, Everyone, seed: 7).Cards.Select(c => c.Id).ToArray();
        var other = DeckMix.FromPicks(picks, Everyone, seed: 8).Cards.Select(c => c.Id).ToArray();

        Assert.Equal(first, again);
        Assert.NotEqual(first.Order(), other.Order());
    }

    [Fact]
    public void A_game_of_the_mix_plays_every_mixed_card_once()
    {
        var mix = DeckMix.FromPicks([FullDeck() with { Id = "full" }, Named("b", 30)], Everyone, seed: 3);
        var settings = DeckMix.SettingsFor(new GameSettings { KindIds = ["finish"], MaxDepth = 1, CardCount = 5 }, mix);

        var state = Game.Start(Couple, settings, [mix], seed: 3);
        var played = new List<string>();
        while (state.Phase != GamePhase.Ended)
        {
            played.Add(state.Current!.Card.Id);
            state = Game.Apply(state, new PassCard(Ani.Id));
        }

        Assert.Equal(mix.Cards.Select(c => c.Id).Order(), played.Order());
    }

    [Fact]
    public void A_deck_counts_only_cards_everyone_can_read()
    {
        var deck = Deck(Card("all", CardKinds.OpenQuestion), Card("hy-en", CardKinds.OpenQuestion, 1, Lang.Hy, Lang.En), Card("hy", CardKinds.OpenQuestion, 1, Lang.Hy));

        Assert.Equal(3, deck.CountReadableBy([Lang.Hy]));
        Assert.Equal(2, deck.CountReadableBy([Lang.Hy, Lang.En]));
        Assert.Equal(1, deck.CountReadableBy([Lang.Hy, Lang.Ru]));
        Assert.Equal(0, Deck(Card("hy", CardKinds.OpenQuestion, 1, Lang.Hy)).CountReadableBy([Lang.Ru]));
    }
}
