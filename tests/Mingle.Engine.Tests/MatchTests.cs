using System.Collections.Immutable;
using static Mingle.Engine.Tests.TestData;

namespace Mingle.Engine.Tests;

public class MatchTests
{
    private static readonly Player[] Four = [.. Enumerable.Range(1, 4).Select(i => new Player($"p{i}", $"P{i}", Lang.En))];

    private static Card[] Prompts(int n) => [.. Enumerable.Range(1, n).Select(i => new Card($"m{i}", CardKinds.MatchPrompt.Id, 1, [],
        ImmutableDictionary<Lang, CardText>.Empty.Add(Lang.En, new CardText("A red fruit"))))];

    [Theory]
    [InlineData("Apple", "apple.")]
    [InlineData("  Green   apple!", "green apple")]
    [InlineData("Խնձորը", "խնձոր")]
    [InlineData("Ёжик", "ежик")]
    public void Answers_match_ignoring_case_spaces_and_punctuation(string a, string b) =>
        Assert.Equal(MatchGame.Key(a), MatchGame.Key(b));

    [Fact]
    public void Each_player_scores_for_every_other_player_with_the_same_answer()
    {
        var game = new MatchGame(Four, Prompts(2), new MatchSettings(2), seed: 1);
        game.Answer("p1", "Apple");
        game.Answer("p2", "apple");
        game.Answer("p3", "Apple!");
        Assert.Empty(game.ForPlayer("p1", Lang.En).Groups); // nothing shows before everyone is in
        game.Answer("p4", "Strawberry");
        Assert.Equal(MatchStage.Revealed, game.Stage);

        var groups = game.Groups;
        Assert.Equal(3, groups[0].PlayerIds.Length);
        var scores = game.Scores.ToDictionary(s => s.PlayerId);
        Assert.Equal(2, scores["p1"].Points);
        Assert.Equal(0, scores["p4"].Points);
        Assert.Equal(1, scores["p4"].Unique);
    }

    [Fact]
    public void The_host_can_join_two_answers_that_mean_the_same()
    {
        var game = new MatchGame(Four, Prompts(1), new MatchSettings(1), seed: 2);
        game.Answer("p1", "Apple");
        game.Answer("p2", "Green apple");
        game.Answer("p3", "Cherry");
        game.Answer("p4", "Apple");
        var green = game.Groups.Single(g => g.Text == "Green apple").Key;
        var apple = game.Groups.Single(g => g.PlayerIds.Length == 2).Key;
        game.Join(green, apple);
        Assert.Equal(3, game.Groups[0].PlayerIds.Length);
        game.Next();
        Assert.Equal(MatchStage.Ended, game.Stage);
        Assert.Equal(2, game.Scores.Single(s => s.PlayerId == "p2").Points);
    }
}
