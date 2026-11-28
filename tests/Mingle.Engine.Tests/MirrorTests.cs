using System.Collections.Immutable;

namespace Mingle.Engine.Tests;

public class MirrorTests
{
    private static readonly Player[] Three = [.. Enumerable.Range(1, 3).Select(i => new Player($"p{i}", $"P{i}", Lang.En))];

    private static Card[] Scales(int n) => [.. Enumerable.Range(1, n).Select(i => new Card($"s{i}", CardKinds.MirrorScale.Id, 1, [],
        ImmutableDictionary<Lang, CardText>.Empty.Add(Lang.En, new CardText("Morning person", ["Night owl"]))))];

    [Fact]
    public void Each_player_takes_the_scale_once_per_lap()
    {
        var game = new MirrorGame(Three, Scales(20), new MirrorSettings(2), seed: 4);
        Assert.Equal(6, game.Count);
        var subjects = new List<string>();
        while (game.Stage != MirrorStage.Ended)
        {
            subjects.Add(game.SubjectId!);
            foreach (var p in Three) game.Place(p.Id, 5);
            game.Next();
        }
        Assert.All(Three, p => Assert.Equal(2, subjects.Count(s => s == p.Id)));
    }

    [Fact]
    public void Placements_stay_secret_until_everyone_has_placed_and_then_score_by_closeness()
    {
        var game = new MirrorGame(Three, Scales(3), new MirrorSettings(1), seed: 1);
        var subject = game.SubjectId!;
        var others = Three.Where(p => p.Id != subject).Select(p => p.Id).ToList();

        game.Place(subject, 2);
        game.Place(others[0], 2);
        Assert.Null(game.ForPlayer(others[0], Lang.En).Reveal);
        game.Place(others[1], 8);

        Assert.Equal(MirrorStage.Revealed, game.Stage);
        var reveal = game.ForPlayer(subject, Lang.En).Reveal!;
        Assert.Equal(2, reveal.Self);
        Assert.Equal(5, reveal.Average);
        Assert.Equal(0, reveal.SelfPoints); // the group put them 3 away on average
        var scores = game.Scores.ToDictionary(s => s.PlayerId);
        Assert.Equal(3, scores[others[0]].Points);
        Assert.Equal(1, scores[others[0]].Bullseyes);
        Assert.Equal(0, scores[others[1]].Points);
        Assert.Equal(3, scores[subject].Gap);
    }

    [Fact]
    public void Knowing_yourself_earns_a_bonus()
    {
        var game = new MirrorGame(Three, Scales(3), new MirrorSettings(1), seed: 2);
        var subject = game.SubjectId!;
        foreach (var p in Three) game.Place(p.Id, p.Id == subject ? 7 : 6);
        Assert.Equal(MirrorGame.SelfBonus, game.Scores.Single(s => s.PlayerId == subject).Points);
    }

    [Fact]
    public void The_host_can_reveal_early_only_once_the_one_on_the_scale_has_placed()
    {
        var game = new MirrorGame(Three, Scales(3), new MirrorSettings(1), seed: 3);
        var subject = game.SubjectId!;
        var other = Three.First(p => p.Id != subject).Id;
        game.Place(other, 4);
        Assert.Throws<MirrorRuleException>(game.Reveal);
        game.Place(subject, 9);
        game.Reveal();
        Assert.Single(game.ForPlayer(subject, Lang.En).Reveal!.Placements);
    }

    [Fact]
    public void Big_gaps_become_the_moments_of_the_game()
    {
        var game = new MirrorGame(Three, Scales(3), new MirrorSettings(1), seed: 5);
        while (game.Stage != MirrorStage.Ended)
        {
            var subject = game.SubjectId!;
            foreach (var p in Three) game.Place(p.Id, p.Id == subject ? 1 : 10);
            game.Next();
        }
        var view = game.ForPlayer("p1", Lang.En);
        Assert.Equal(3, view.Moments.Length);
        Assert.All(view.Moments, m => Assert.Equal("Night owl", m.Right));
    }

    [Fact]
    public void Values_must_be_on_the_scale()
    {
        var game = new MirrorGame(Three, Scales(3), new MirrorSettings(1), seed: 1);
        Assert.Throws<MirrorRuleException>(() => game.Place("p1", 0));
        Assert.Throws<MirrorRuleException>(() => game.Place("p1", 11));
        Assert.Throws<MirrorRuleException>(() => game.Place("nobody", 5));
    }
}
