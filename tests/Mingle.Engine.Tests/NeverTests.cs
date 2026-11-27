using System.Collections.Immutable;
using static Mingle.Engine.Tests.TestData;

namespace Mingle.Engine.Tests;

public class NeverTests
{
    private static readonly Player[] Four = [.. Enumerable.Range(1, 4).Select(i => new Player($"p{i}", $"P{i}", Lang.En))];

    private static Card Statement(string id, int depth) => new(id, CardKinds.NeverHaveI.Id, depth, [],
        ImmutableDictionary<Lang, CardText>.Empty.Add(Lang.En, new CardText($"I have never {id}")));

    private static Card[] Mixed => [Statement("a", 3), Statement("b", 1), Statement("c", 2), Statement("d", 1), Statement("e", 3)];

    [Fact]
    public void Levels_are_chosen_and_the_game_goes_from_light_to_bold()
    {
        var light = new NeverGame(Four, Mixed, new NeverSettings(10), seed: 1);
        Assert.Equal(3, light.Count); // light and personal by default
        var all = new NeverGame(Four, Mixed, new NeverSettings(10, [1, 2, 3]), seed: 1);
        var depths = new List<int>();
        while (all.Stage != NeverStage.Ended) { depths.Add(all.Card!.Depth); all.Next(); }
        Assert.Equal([1, 1, 2, 3, 3], depths);
    }

    [Fact]
    public void Online_the_count_shows_first_and_right_names_score()
    {
        var game = new NeverGame(Four, Mixed, new NeverSettings(5), seed: 2);
        game.Answer("p1", true);
        game.Answer("p2", true);
        game.Answer("p3", false);
        Assert.Empty(game.ForPlayer("p4", Lang.En).HaveIds);
        game.Answer("p4", false);
        Assert.Equal(NeverStage.Guessing, game.Stage);

        var p3 = game.ForPlayer("p3", Lang.En);
        Assert.Equal(2, p3.HaveCount);
        Assert.Equal(2, p3.PicksNeeded);
        Assert.Equal(1, game.ForPlayer("p1", Lang.En).PicksNeeded); // p1 knows about themselves
        Assert.Empty(p3.HaveIds);
        Assert.Throws<NeverRuleException>(() => game.Guess("p3", ["p1"]));

        game.Guess("p1", ["p2"]);
        game.Guess("p2", ["p3"]);
        game.Guess("p3", ["p1", "p2"]);
        game.Guess("p4", ["p1", "p3"]);
        Assert.Equal(NeverStage.Revealed, game.Stage);
        Assert.Contains(game.Storyteller, new[] { "p1", "p2" });

        var scores = game.Scores.ToDictionary(s => s.PlayerId);
        Assert.Equal(2, scores["p3"].Points);
        Assert.Equal(1, scores["p4"].Points);
        Assert.Equal(1, scores["p1"].Points);
        Assert.Equal(0, scores["p2"].Points);
        Assert.Equal(1, scores["p1"].Done);
    }

    [Fact]
    public void Nothing_to_guess_when_nobody_or_everybody_has()
    {
        var game = new NeverGame(Four, Mixed, new NeverSettings(5), seed: 3);
        foreach (var p in Four) game.Answer(p.Id, false);
        Assert.Equal(NeverStage.Revealed, game.Stage);
        Assert.Null(game.Storyteller);
    }

    [Fact]
    public void On_one_phone_answers_are_open_and_can_be_taken_back()
    {
        var game = new NeverGame(Trio, Mixed, new NeverSettings(5), seed: 4, secret: false);
        game.Answer(Ani.Id, true);
        game.Answer(Arman.Id, true);
        game.Answer(Arman.Id, false);
        Assert.Equal([Ani.Id], game.ForPlayer(Ani.Id, Lang.En).HaveIds.ToArray());
        game.StartGuessing();
        Assert.Equal(NeverStage.Revealed, game.Stage);
        Assert.Equal(Ani.Id, game.Storyteller);
        game.Next();
        Assert.Equal(1, game.Scores.Single(s => s.PlayerId == Ani.Id).Done);
    }
}
