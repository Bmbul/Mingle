using static Mingle.Engine.Tests.TestData;

namespace Mingle.Engine.Tests;

public class ViewTests
{
    [Fact]
    public void Nobody_sees_other_answers_before_the_reveal()
    {
        var state = Start(Only(CardKinds.FinishSentence));
        state = Game.Apply(state, new SubmitAnswer("ani", Answer.FromText("secret")));

        var arman = GameViews.ForPlayer(state, "arman");
        var ani = GameViews.ForPlayer(state, "ani");
        var table = GameViews.ForTable(state, Lang.En);

        Assert.Empty(arman.RevealedAnswers);
        Assert.Null(arman.MyAnswer);
        Assert.True(arman.MustAnswer);
        Assert.Equal("secret", ani.MyAnswer!.Text);
        Assert.Empty(table.RevealedAnswers);
        Assert.Null(table.MyAnswer);
        Assert.Equal(1, table.AnsweredCount);
    }

    [Fact]
    public void Everyone_sees_all_answers_after_the_reveal()
    {
        var state = AnswerAll(Start(Only(CardKinds.FinishSentence)));

        var table = GameViews.ForTable(state, Lang.En);

        Assert.Equal(2, table.RevealedAnswers.Length);
        Assert.False(GameViews.ForPlayer(state, "ani").MustAnswer);
    }

    [Fact]
    public void Each_player_reads_the_card_in_their_own_language()
    {
        var state = Start(Only(CardKinds.OpenQuestion, AnswerMode.Talk));

        Assert.EndsWith("in hy", GameViews.ForPlayer(state, "ani").Prompt);
        Assert.EndsWith("in en", GameViews.ForPlayer(state, "arman").Prompt);
        Assert.EndsWith("in ru", GameViews.ForTable(state, Lang.Ru).Prompt);
    }

    [Fact]
    public void Choices_and_votes_are_shown_as_words_not_numbers()
    {
        var wyr = AnswerAll(Start(Only(CardKinds.WouldYouRather)));
        Assert.All(GameViews.ForTable(wyr, Lang.En).RevealedAnswers, a => Assert.Equal("B", a.ChoiceText));

        var likely = AnswerAll(Start(Only(CardKinds.MostLikelyTo)));
        Assert.All(GameViews.ForTable(likely, Lang.En).RevealedAnswers, a => Assert.Equal("Ani", a.VotedPlayerName));
    }

    [Fact]
    public void The_card_total_is_capped_by_the_matching_cards()
    {
        var deck = Deck(Card("a", CardKinds.OpenQuestion), Card("b", CardKinds.OpenQuestion));
        var state = Start(Only(CardKinds.OpenQuestion, AnswerMode.Talk) with { CardCount = 20 }, deck: deck);

        Assert.Equal(2, GameViews.ForTable(state, Lang.En).CardCount);
    }
}
