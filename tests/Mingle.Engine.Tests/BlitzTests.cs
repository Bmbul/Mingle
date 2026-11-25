using static Mingle.Engine.Tests.TestData;

namespace Mingle.Engine.Tests;

public class BlitzTests
{
    private static Card Word(string id, params Lang[] langs) => Card(id, CardKinds.Association, 1, langs);

    [Fact]
    public void The_pool_has_only_quick_cards_the_player_can_read()
    {
        var deck = Deck(Word("w1"), Word("w-en", Lang.En), Card("wyr", CardKinds.WouldYouRather), Card("open", CardKinds.OpenQuestion));

        var armenian = Blitz.Pool([deck, deck], Lang.Hy).Select(c => c.Id).ToArray();

        Assert.Equal(["w1"], armenian);
        Assert.Equal(2, Blitz.Pool([deck], Lang.En).Length);
    }

    [Fact]
    public void Cards_do_not_repeat_until_every_card_was_seen()
    {
        var pool = Blitz.Pool([Deck(Word("a"), Word("b"), Word("c"))], Lang.En);
        var seen = new HashSet<string>();
        var random = new Random(1);

        var firstRound = Enumerable.Range(0, 3).Select(_ => Blitz.Next(pool, seen, random)!.Id).ToArray();
        var fourth = Blitz.Next(pool, seen, random);

        Assert.Equal(["a", "b", "c"], firstRound.Order().ToArray());
        Assert.NotNull(fourth); // the pile starts over
        Assert.Null(Blitz.Next([], seen, random));
    }

    [Fact]
    public void Ranking_puts_the_most_answers_first_and_keeps_ties_in_play_order()
    {
        var ranking = Blitz.Ranking([(Ani, 7), (Arman, 9), (Lilit, 7)]);

        Assert.Equal([Arman, Ani, Lilit], ranking.Select(r => r.Player).ToArray());
    }

    [Fact]
    public void Association_cards_never_reach_a_normal_game()
    {
        var deck = Deck([.. Enumerable.Range(1, 5).Select(i => Word($"w{i}")), Card("open", CardKinds.OpenQuestion)]);

        Assert.Equal(["open"], Game.BuildPool([deck], new GameSettings(), Couple).Select(c => c.Id).ToArray());
        Assert.Equal(1, deck.CountReadableBy([Lang.En]));
        Assert.Equal(5, deck.CountReadableBy([Lang.En], [CardKinds.Association]));
        Assert.DoesNotContain(DeckMix.FromPicks([deck with { Id = "d" }], [Lang.En], 1).Cards, c => c.KindId == "word");
        Assert.DoesNotContain(TopicDeal.FromLibrary([deck], [], [Lang.En], 1).Cards, c => c.KindId == "word");
    }

    [Fact]
    public void A_deck_is_either_blitz_or_questions()
    {
        Assert.True(Deck(Word("a"), Word("b")).IsBlitz);
        Assert.False(Deck(Card("q", CardKinds.OpenQuestion)).IsBlitz);
        Assert.True(Deck(Word("a"), Card("q", CardKinds.OpenQuestion)).MixesBlitzAndQuestions);
        Assert.False(Deck(Word("a")).MixesBlitzAndQuestions);
    }
}
