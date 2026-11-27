using static Mingle.Engine.Tests.TestData;

namespace Mingle.Engine.Tests;

public class GuessTests
{
    private static Card Guess(string id) => new(id, CardKinds.GuessMyAnswer.Id, 1, ["travel"],
        System.Collections.Immutable.ImmutableDictionary<Lang, CardText>.Empty
            .Add(Lang.En, new CardText("How many countries has {@} been to?"))
            .Add(Lang.Hy, new CardText("Քանի՞ երկրում է եղել {@ը}։")));

    private static Card[] Cards(int n) => [.. Enumerable.Range(1, n).Select(i => Guess($"g{i}"))];

    [Fact]
    public void Question_games_no_longer_play_guess_cards()
    {
        Assert.DoesNotContain(CardKinds.GuessMyAnswer, CardKinds.Regular);
        Assert.Equal(DeckStyle.Guess, Deck(Guess("g")).Style);
    }

    [Fact]
    public void The_turn_goes_round_and_the_card_names_the_player()
    {
        var game = new GuessGame(Trio, Cards(5), new GuessSettings(4), seed: 1);
        Assert.Equal(4, game.Count);
        Assert.Equal("Քանի՞ երկրում է եղել Ani-ն։", game.ForPlayer(Ani.Id, Lang.Hy).Prompt);
        Assert.True(game.ForPlayer(Ani.Id, Lang.En).YourTurn);
        game.Next();
        Assert.Equal(Arman.Id, game.TurnPlayer.Id);
        Assert.Equal("How many countries has Arman been to?", game.ForPlayer(Lilit.Id, Lang.En).Prompt);
    }

    [Fact]
    public void Online_answers_stay_hidden_until_everyone_has_written()
    {
        var game = new GuessGame(Trio, Cards(3), new GuessSettings(3), seed: 2);
        game.Write(Ani.Id, "12");
        game.Write(Arman.Id, "8");
        var view = game.ForPlayer(Lilit.Id, Lang.En);
        Assert.Empty(view.Answers);
        Assert.Equal([Ani.Id, Arman.Id], view.WrittenIds.ToArray());

        game.Write(Lilit.Id, "15");
        Assert.Equal(GuessStage.Marking, game.Stage);
        var answers = game.ForPlayer(Lilit.Id, Lang.En).Answers;
        Assert.Equal(Ani.Id, answers[0].PlayerId); // the player's own answer first
        Assert.True(answers[0].IsTurnPlayer);
        Assert.Equal(3, answers.Length);
    }

    [Fact]
    public void Marked_guessers_score_and_the_rest_count_as_missed()
    {
        var game = new GuessGame(Trio, Cards(3), new GuessSettings(3), seed: 3, written: false);
        Assert.Throws<GuessRuleException>(() => game.Write(Arman.Id, "x"));
        game.Reveal();
        Assert.Throws<GuessRuleException>(() => game.Mark(Ani.Id, true)); // the player cannot be right about themselves
        game.Mark(Arman.Id, true);
        game.Mark(Lilit.Id, true);
        game.Mark(Lilit.Id, false);
        game.Next();

        var scores = game.Scores.ToDictionary(s => s.PlayerId);
        Assert.Equal(1, scores[Arman.Id].Points);
        Assert.Equal(0, scores[Lilit.Id].Points);
        Assert.Equal(1, scores[Ani.Id].Missed);
    }

    [Fact]
    public void Ending_early_counts_the_card_being_marked()
    {
        var game = new GuessGame(Couple, Cards(5), new GuessSettings(5), seed: 4, written: false);
        game.Reveal();
        game.Mark(Arman.Id, true);
        game.End();
        Assert.Equal(GuessStage.Ended, game.Stage);
        Assert.Equal(1, game.Scores.Single(s => s.PlayerId == Arman.Id).Points);
    }

    [Fact]
    public void A_game_needs_guess_cards_and_two_players()
    {
        Assert.Throws<GuessRuleException>(() => new GuessGame(Trio, [Card("o", CardKinds.OpenQuestion)], new GuessSettings(), 1));
        Assert.Throws<GuessRuleException>(() => new GuessGame([Ani], Cards(2), new GuessSettings(), 1));
    }
}

public class RankTests
{
    private static Card Rank(string id) => new(id, CardKinds.RankIt.Id, 1, [],
        System.Collections.Immutable.ImmutableDictionary<Lang, CardText>.Empty.Add(Lang.En,
            new CardText("How would {@} rank these, favourite first?", ["Pizza", "Dolma", "Sushi", "Khorovats"])));

    [Fact]
    public void Online_rank_cards_score_a_point_per_item_in_the_right_place()
    {
        var game = new GuessGame(TestData.Trio, [Rank("r1"), Rank("r2")], new GuessSettings(2), seed: 1);
        Assert.True(game.IsRank);
        Assert.Equal(["Pizza", "Dolma", "Sushi", "Khorovats"], game.ForPlayer(TestData.Arman.Id, Lang.En).Items.ToArray());
        Assert.Throws<GuessRuleException>(() => game.Write(TestData.Ani.Id, "0,0,1,2"));

        game.Write(TestData.Ani.Id, "3,0,1,2");    // the player's real order
        game.Write(TestData.Arman.Id, "3,0,2,1");  // two in place
        game.Write(TestData.Lilit.Id, "3,0,1,2");  // all four
        var answers = game.ForPlayer(TestData.Arman.Id, Lang.En).Answers;
        Assert.Equal(2, answers.Single(a => a.PlayerId == TestData.Arman.Id).Matches);
        Assert.Throws<GuessRuleException>(() => game.Mark(TestData.Arman.Id, true));

        game.Next();
        var scores = game.Scores.ToDictionary(s => s.PlayerId);
        Assert.Equal(2, scores[TestData.Arman.Id].Points);
        Assert.Equal(4, scores[TestData.Lilit.Id].Points);
        Assert.Equal(1, scores[TestData.Ani.Id].Missed);
    }

    [Fact]
    public void On_one_phone_rank_cards_are_marked_like_the_others()
    {
        var game = new GuessGame(TestData.Couple, [Rank("r1")], new GuessSettings(1), seed: 1, written: false);
        game.Reveal();
        game.Mark(TestData.Arman.Id, true);
        game.Next();
        Assert.Equal(1, game.Scores.Single(s => s.PlayerId == TestData.Arman.Id).Points);
    }
}
