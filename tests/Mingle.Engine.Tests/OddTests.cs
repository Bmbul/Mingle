using System.Collections.Immutable;
using static Mingle.Engine.Tests.TestData;

namespace Mingle.Engine.Tests;

public class OddTests
{
    private static readonly Player[] Six = [.. Enumerable.Range(1, 6).Select(i => new Player($"p{i}", $"P{i}", Lang.En))];

    private static Card Pair(string id) => new(id, CardKinds.OddPair.Id, 1, [],
        ImmutableDictionary<Lang, CardText>.Empty.Add(Lang.En, new CardText("Your favourite food?", ["A food you'd eat on a diet?"])));

    private static Card[] Pairs(int n) => [.. Enumerable.Range(1, n).Select(i => Pair($"o{i}"))];

    [Fact]
    public void One_player_gets_the_different_question_and_cannot_tell()
    {
        var game = new OddGame(Trio, Pairs(3), new OddSettings(3), seed: 1);
        var odd = Assert.Single(game.OddIds);
        Assert.Equal("A food you'd eat on a diet?", game.ForPlayer(odd, Lang.En).MyQuestion);
        Assert.All(Trio.Where(p => p.Id != odd), p => Assert.Equal("Your favourite food?", game.ForPlayer(p.Id, Lang.En).MyQuestion));
        // Nothing in a player's view says who is different before the reveal.
        Assert.Empty(game.ForPlayer(odd, Lang.En).OddIds);
        Assert.Null(game.ForPlayer(odd, Lang.En).OddQuestion);
    }

    [Fact]
    public void Finding_the_odd_one_scores_and_escaping_scores_double()
    {
        var game = new OddGame(Trio, Pairs(3), new OddSettings(3), seed: 2);
        var odd = game.OddIds[0];
        var others = Trio.Where(p => p.Id != odd).Select(p => p.Id).ToList();
        foreach (var p in Trio) game.Answer(p.Id, "pizza");
        Assert.Equal(OddStage.Voting, game.Stage);
        Assert.Equal(3, game.ForPlayer(odd, Lang.En).Answers.Length);
        Assert.Throws<OddRuleException>(() => game.Vote(odd, odd));

        game.Vote(others[0], odd);       // found it
        game.Vote(others[1], others[0]); // wrong
        game.Vote(odd, others[0]);       // the odd one bluffs
        Assert.Equal(OddStage.Revealed, game.Stage);
        // The top vote went to others[0], so the odd one escaped.
        Assert.False(game.Caught);
        var scores = game.Scores.ToDictionary(s => s.PlayerId);
        Assert.Equal(1, scores[others[0]].Points);
        Assert.Equal(2, scores[odd].Points);
        Assert.Equal([odd], game.ForPlayer(others[1], Lang.En).OddIds.ToArray());
    }

    [Fact]
    public void Big_groups_can_have_two_odd_ones()
    {
        Assert.Equal(2, new OddGame(Six, Pairs(2), new OddSettings(2, TwoOdd: true), seed: 3).OddIds.Length);
        Assert.Single(new OddGame(Trio, Pairs(2), new OddSettings(2, TwoOdd: true), seed: 3).OddIds);
    }

    [Fact]
    public void On_one_phone_the_group_accuses_and_rounds_move_on()
    {
        var game = new OddGame(Trio, Pairs(2), new OddSettings(2), seed: 4, votes: false);
        Assert.Throws<OddRuleException>(() => game.Answer(Ani.Id, "x"));
        game.StartVoting();
        game.Accuse([game.OddIds[0]]);
        Assert.True(game.Caught);
        game.Next();
        Assert.Equal(OddStage.Answering, game.Stage);
        Assert.Equal(1, game.Index);
        game.StartVoting();
        game.Accuse([Trio.First(p => !game.IsOdd(p.Id)).Id]);
        Assert.False(game.Caught);
        game.Next();
        Assert.Equal(OddStage.Ended, game.Stage);
    }
}
