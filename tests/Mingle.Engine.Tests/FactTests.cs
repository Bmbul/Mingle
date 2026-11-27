using static Mingle.Engine.Tests.TestData;

namespace Mingle.Engine.Tests;

public class FactTests
{
    private static readonly Player[] Three = [new("a", "Ani", Lang.Hy), new("b", "Bob", Lang.En), new("c", "Cat", Lang.Ru)];

    private static Card[] Prompts(int count) => [.. Enumerable.Range(1, count).Select(i => Card($"f{i}", CardKinds.FactPrompt))];

    private static FactGame OneTruth(bool votes = true) => new(Three, new FactSettings(FactGameKind.OneTruth), Prompts(5), seed: 7, votes);

    private static void WriteAll(FactGame game)
    {
        foreach (var p in Three) game.Write(p.Id, [$"{p.Id} true", $"{p.Id} lie 1", $"{p.Id} lie 2"], marked: 0);
    }

    [Fact]
    public void One_truth_starts_once_everyone_has_written_and_keeps_the_answer_secret()
    {
        var game = OneTruth();
        game.Write("a", ["1", "2", "3"], 1);
        Assert.Equal(FactStage.Writing, game.Stage);
        Assert.True(game.ForPlayer("a", Lang.En).YouWrote);
        Assert.False(game.ForPlayer("b", Lang.En).YouWrote);

        game.Write("b", ["1", "2", "3"], 0);
        game.Write("c", ["1", "2", "3"], 2);
        Assert.Equal(FactStage.Showing, game.Stage);
        Assert.Equal(3, game.Items.Count);

        var view = game.ForPlayer("a", Lang.En).Item!;
        Assert.NotNull(view.AuthorId); // the spotlight player is named
        Assert.Null(view.Answer);
        Assert.Empty(view.Votes);
    }

    [Fact]
    public void Each_player_gets_a_different_prompt_and_can_swap_it()
    {
        var game = OneTruth();
        var prompts = Three.Select(p => game.PromptsFor(p.Id).Single()).ToList();
        Assert.Equal(3, prompts.Distinct().Count());

        var before = game.PromptsFor("a").Single();
        game.SwapPrompt("a");
        Assert.NotEqual(before, game.PromptsFor("a").Single());
    }

    [Fact]
    public void Right_votes_score_for_the_voter_and_wrong_votes_for_the_author()
    {
        var game = OneTruth();
        WriteAll(game);
        var item = game.Current!;
        var right = item.Marked.ToString();
        var wrong = ((item.Marked + 1) % 3).ToString();
        var voters = game.VotersFor(item);
        Assert.DoesNotContain(item.AuthorId, voters);
        Assert.Throws<FactRuleException>(() => game.Vote(item.AuthorId, right));

        game.Vote(voters[0], right);
        Assert.Equal(FactStage.Showing, game.Stage);
        game.Vote(voters[1], wrong);
        Assert.Equal(FactStage.Revealed, game.Stage);

        var scores = game.Scores().ToDictionary(s => s.PlayerId);
        Assert.Equal(1, scores[voters[0]].RightGuesses);
        Assert.Equal(1, scores[item.AuthorId].Fooled);
        Assert.Equal(0, scores[voters[1]].Points);
        // The texts were shuffled, but the marked one is still the true fact.
        Assert.EndsWith("true", item.Texts[item.Marked]);
    }

    [Fact]
    public void On_one_phone_nobody_votes_and_the_group_reveals()
    {
        var game = OneTruth(votes: false);
        WriteAll(game);
        Assert.Throws<FactRuleException>(() => game.Vote("a", "0"));
        game.Reveal();
        Assert.NotNull(game.ForPlayer("a", Lang.En).Item!.Answer);
        game.Next();
        game.Next(); // skipping the reveal is fine at the table
        game.Next();
        Assert.Equal(FactStage.Ended, game.Stage);
    }

    [Fact]
    public void Whose_fact_hides_the_author_until_the_reveal()
    {
        var game = new FactGame(Three, new FactSettings(FactGameKind.WhoseFact, FactsEach: 2), Prompts(5), seed: 3);
        // Everyone writes to the same prompts.
        Assert.Single(Three.Select(p => string.Join(",", game.PromptsFor(p.Id))).Distinct());
        foreach (var p in Three) game.Write(p.Id, [$"{p.Id}-1", $"{p.Id}-2"]);
        Assert.Equal(6, game.Items.Count);
        // No author twice in a row.
        Assert.All(game.Items.Zip(game.Items.Skip(1)), pair => Assert.NotEqual(pair.First.AuthorId, pair.Second.AuthorId));

        var item = game.Current!;
        Assert.Null(game.ForPlayer("a", Lang.En).Item!.AuthorId);
        Assert.True(game.ForPlayer(item.AuthorId, Lang.En).Item!.YouWrote);

        // Everyone votes, the author bluffs; nobody may pick themselves.
        Assert.Throws<FactRuleException>(() => game.Vote("a", "a"));
        foreach (var p in Three) game.Vote(p.Id, p.Id == item.AuthorId ? Three.First(o => o.Id != p.Id).Id : item.AuthorId);
        Assert.Equal(FactStage.Revealed, game.Stage);
        Assert.Equal(item.AuthorId, game.ForPlayer("a", Lang.En).Item!.AuthorId);

        var scores = game.Scores().ToDictionary(s => s.PlayerId);
        Assert.Equal(0, scores[item.AuthorId].Points); // the bluff scores nothing, and nobody was fooled
        Assert.All(Three.Where(p => p.Id != item.AuthorId), p => Assert.Equal(1, scores[p.Id].RightGuesses));
    }

    [Fact]
    public void Whose_fact_can_keep_every_author_for_the_end()
    {
        var game = new FactGame(Three, new FactSettings(FactGameKind.WhoseFact, RevealEachFact: false), Prompts(2), seed: 1);
        foreach (var p in Three) game.Write(p.Id, [$"{p.Id} fact"]);

        var first = game.Current!;
        foreach (var p in Three) game.Vote(p.Id, Three.First(o => o.Id != p.Id && o.Id != first.AuthorId)?.Id ?? first.AuthorId);
        // Straight on to the next fact, nothing given away.
        Assert.Equal(FactStage.Showing, game.Stage);
        Assert.Equal(1, game.Index);
        Assert.Empty(game.ForPlayer("a", Lang.En).Scores);

        game.End();
        var end = game.ForPlayer("a", Lang.En);
        Assert.Single(end.Recap); // only the fact that came up before the game ended
        Assert.NotEmpty(end.Scores);
    }

    [Fact]
    public void The_host_can_start_without_someone_who_never_wrote()
    {
        var game = OneTruth();
        Assert.Throws<FactRuleException>(game.Begin);
        game.Write("a", ["1", "2", "3"], 0);
        game.Write("b", ["1", "2", "3"], 0);
        game.Begin();
        Assert.Equal(2, game.Items.Count);
        Assert.Throws<FactRuleException>(() => game.Write("c", ["1", "2", "3"], 0));
    }

    [Fact]
    public void Facts_must_be_written_in_full()
    {
        var game = OneTruth();
        Assert.Throws<FactRuleException>(() => game.Write("a", ["1", "", "3"], 0));
        Assert.Throws<FactRuleException>(() => game.Write("a", ["1", "2"], 0));
        Assert.Throws<FactRuleException>(() => game.Write("a", ["1", "2", "3"], 3));
        Assert.Throws<FactRuleException>(() => game.Write("a", ["1", "2", new string('x', FactGame.MaxFactLength + 1)], 0));
    }
}
