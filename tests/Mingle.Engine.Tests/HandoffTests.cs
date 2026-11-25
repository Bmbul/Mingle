using static Mingle.Engine.Tests.TestData;

namespace Mingle.Engine.Tests;

public class HandoffTests
{
    private static Card Pass(string id, params Lang[] langs) => Card(id, CardKinds.Handoff, 1, langs);

    [Fact]
    public void The_pool_has_only_handoff_cards_everyone_can_read()
    {
        var deck = Deck(Pass("p1"), Pass("p-en", Lang.En), Card("open", CardKinds.OpenQuestion), Card("w", CardKinds.Association));

        Assert.Equal(["p1"], Handoffs.Pool([deck, deck], [Lang.Hy, Lang.En]).Select(c => c.Id).ToArray());
        Assert.Equal(2, Handoffs.Pool([deck], [Lang.En]).Length);
    }

    [Fact]
    public void Guess_choices_hold_the_real_card_and_two_different_decoys()
    {
        var pool = Handoffs.Pool([Deck(Pass("a"), Pass("b"), Pass("c"), Pass("d"))], [Lang.En]);

        for (var seed = 0; seed < 20; seed++)
        {
            var choices = Handoffs.GuessChoices(pool[1], pool, new Random(seed));
            Assert.Equal(3, choices.Length);
            Assert.Contains(pool[1], choices);
            Assert.Equal(3, choices.Select(c => c.Id).Distinct().Count());
        }
        Assert.Single(Handoffs.GuessChoices(pool[0], [pool[0]], new Random(1)));
    }

    [Fact]
    public void The_trail_puts_the_most_passes_first_and_counts_right_guesses()
    {
        Handoff[] passes =
        [
            new(Ani.Id, Lilit.Id, "a", true, true),
            new(Lilit.Id, Arman.Id, "b", false, false),
            new(Arman.Id, Lilit.Id, "c", true, true),
        ];

        var trail = Handoffs.Trail(Trio, passes);

        Assert.Equal([Lilit, Arman, Ani], trail.Select(t => t.Player).ToArray());
        Assert.Equal(2, trail[0].RightGuesses);
        Assert.Equal(["a", "c"], trail[0].Received.Select(h => h.CardId).ToArray());
        Assert.Empty(trail[2].Received);
    }

    [Fact]
    public void Handoff_cards_never_reach_a_normal_game_and_decks_keep_one_style()
    {
        var mixed = Deck(Pass("p"), Card("open", CardKinds.OpenQuestion));

        Assert.Equal(["open"], Game.BuildPool([mixed], new GameSettings(), Couple).Select(c => c.Id).ToArray());
        Assert.True(mixed.MixesStyles);
        Assert.Equal(DeckStyle.Handoff, Deck(Pass("p")).Style);
    }

    [Fact]
    public void The_coin_is_even_and_avoids_long_streaks()
    {
        var random = new Random(7);
        var tosses = new List<bool>();
        var (shown, longest, streak) = (0, 0, 0);
        bool? last = null;
        for (var i = 0; i < 10_000; i++)
        {
            var show = Handoffs.CoinShows(tosses, random);
            tosses.Add(show);
            if (show) shown++;
            streak = show == last ? streak + 1 : 1;
            longest = Math.Max(longest, streak);
            last = show;
        }

        Assert.InRange(shown, 4_900, 5_100);
        Assert.True(longest <= 4, $"A streak of {longest}.");
    }
}

public class CringeOrFineTests
{
    [Fact]
    public void A_cringe_card_holds_only_a_text_and_the_kind_adds_both_answers()
    {
        var deck = DeckJson.Parse("""
            { "id": "d", "name": { "en": "D" }, "cards": [
              { "id": "c1", "kind": "cringe", "depth": 1, "topics": [],
                "text": { "en": "Clapping when the plane lands", "hy": "Ծափահարել վայրէջքին" } } ] }
            """);
        var card = deck.Cards[0];

        Assert.Equal(["Ամոթ է", "Նորմալ է"], card.TextFor(Lang.Hy).Options.ToArray());
        Assert.Equal(["Cringe", "Fine"], card.TextFor(Lang.En).Options.ToArray());
        Assert.Equal("Clapping when the plane lands", card.TextFor(Lang.En).Text);
        Assert.False(card.Kind.CardsHaveOptions);
        Assert.True(CardKinds.WouldYouRather.CardsHaveOptions);
        Assert.Throws<FormatException>(() => DeckJson.Parse("""
            { "id": "d", "name": { "en": "D" }, "cards": [
              { "id": "c1", "kind": "cringe", "depth": 1, "topics": [],
                "text": { "en": "x" }, "options": { "en": ["a", "b"] } } ] }
            """));
    }
}
