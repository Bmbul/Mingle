using static Mingle.Engine.Tests.TestData;

namespace Mingle.Engine.Tests;

public class UpcomingTests
{
    [Theory]
    [InlineData(CardOrder.LightToDeep, false)]
    [InlineData(CardOrder.Shuffled, false)]
    [InlineData(CardOrder.LightToDeep, true)]
    public void The_card_shown_underneath_is_the_one_that_comes_next(CardOrder order, bool pass)
    {
        var state = Start(Only(CardKinds.OpenQuestion, AnswerMode.OutLoud) with { CardCount = 8, Order = order }, Trio, FullDeck(), seed: 11);
        for (var i = 0; i < 7; i++)
        {
            var shown = GameViews.ForTable(state, Lang.En).Next;
            Assert.NotNull(shown);
            var me = state.Players[0].Id;
            state = Game.Apply(state, pass ? new PassCard(me) : new NextCard(me));
            Assert.Equal(shown!.Prompt, state.Current!.Card.TextFor(Lang.En).Text);
        }
        Assert.Null(GameViews.ForTable(state, Lang.En).Next); // the last card has nothing under it
    }
}
