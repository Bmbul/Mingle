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
}
