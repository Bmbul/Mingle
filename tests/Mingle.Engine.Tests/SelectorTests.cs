using static Mingle.Engine.Tests.TestData;

namespace Mingle.Engine.Tests;

public class SelectorTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(7, 1)]
    [InlineData(8, 2)]
    [InlineData(14, 2)]
    [InlineData(15, 3)]
    [InlineData(20, 3)]
    [InlineData(25, 3)]
    public void Depth_ramps_from_light_to_deep_over_the_game(int cardNumber, int expectedDepth)
    {
        var settings = new GameSettings { MinDepth = 1, MaxDepth = 3, CardCount = 20 };
        Assert.Equal(expectedDepth, CardSelector.TargetDepth(cardNumber, settings));
    }

    [Fact]
    public void A_single_depth_never_ramps()
    {
        var settings = new GameSettings { MinDepth = 2, MaxDepth = 2, CardCount = 20 };
        Assert.Equal(2, CardSelector.TargetDepth(20, settings));
    }

    [Fact]
    public void The_first_card_is_light_when_light_cards_exist()
    {
        for (var seed = 0; seed < 20; seed++)
        {
            var state = Start(new GameSettings { AnswerMode = AnswerMode.Talk }, seed: seed);
            Assert.Equal(1, state.Current!.Card.Depth);
        }
    }

    [Fact]
    public void When_the_target_depth_is_used_up_a_shallower_card_comes_first()
    {
        var pool = Deck(Card("light", CardKinds.OpenQuestion, 1), Card("deep", CardKinds.OpenQuestion, 3)).Cards;
        var context = new SelectionContext(pool, new HashSet<string>(), CardNumber: 10,
            new GameSettings { MinDepth = 1, MaxDepth = 3, CardCount = 20 }, Seed: 1);

        Assert.Equal("light", CardSelector.Instance.Pick(context)!.Id);
    }

    [Fact]
    public void Returns_null_when_everything_was_played()
    {
        var pool = Deck(Card("a", CardKinds.OpenQuestion)).Cards;
        var context = new SelectionContext(pool, new HashSet<string> { "a" }, 2, new GameSettings(), 1);

        Assert.Null(CardSelector.Instance.Pick(context));
    }

    [Fact]
    public void Shuffled_order_can_open_with_any_depth()
    {
        var firstDepths = Enumerable.Range(0, 40)
            .Select(seed => Start(new GameSettings { AnswerMode = AnswerMode.Talk, Order = CardOrder.Shuffled }, seed: seed).Current!.Card.Depth)
            .ToHashSet();

        Assert.Equal([1, 2, 3], firstDepths.Order());
    }

    [Fact]
    public void Shuffled_order_still_deals_every_card_once()
    {
        var deck = FullDeck();
        var state = Start(new GameSettings { AnswerMode = AnswerMode.Talk, Order = CardOrder.Shuffled, CardCount = deck.Cards.Length }, deck: deck);
        while (state.Current is not null) state = Game.Apply(state, new PassCard("ani"));

        Assert.Equal(deck.Cards.Length, state.History.Select(h => h.Card.Id).Distinct().Count());
    }
}
