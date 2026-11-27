using System.Collections.Immutable;

namespace Mingle.Engine;

/// <summary>The two fact games.</summary>
public enum FactGameKind
{
    /// <summary>Everyone writes three facts about themselves, one true (or one false); the others find it.</summary>
    OneTruth,
    /// <summary>Everyone writes a fact about themselves; the facts come up one by one and everyone guesses whose it is.</summary>
    WhoseFact,
    /// <summary>Everyone writes facts about themselves, each true or made up; the others guess which.</summary>
    TrueOrFalse,
}

/// <summary>Writing: everyone writes. Showing: one item is up (and votes come in). Revealed: its answer is out.</summary>
public enum FactStage { Writing, Showing, Revealed, Ended }

/// <param name="FindTheLie">One truth: two truths and a lie, and the lie is what you look for.</param>
/// <param name="FactsEach">Whose fact and True or false: how many facts every player writes.</param>
/// <param name="RevealEachFact">Whose fact: show the author after every fact; otherwise all authors come out at the end.</param>
public sealed record FactSettings(FactGameKind Kind = FactGameKind.OneTruth, bool FindTheLie = false, int FactsEach = 1, bool RevealEachFact = true);

/// <summary>Fact games break a rule (wrong stage, a second vote, an empty fact).</summary>
public sealed class FactRuleException(string message) : Exception(message);

/// <summary>One thing shown to the group: a player's three facts (One truth) or one fact (Whose fact).</summary>
/// <param name="Marked">One truth: the position of the true fact (the lie in "find the lie"). True or false: 1 when
/// the fact is true, 0 when made up. Whose fact: 0.</param>
public sealed record FactItem(string AuthorId, string? PromptId, ImmutableArray<string> Texts, int Marked)
{
    public const string True = "true";
    public const string False = "false";

    /// <summary>What a right vote says: the position for One truth, "true"/"false" for True or false, the author for Whose fact.</summary>
    public string Answer(FactGameKind kind) => kind switch
    {
        FactGameKind.OneTruth => Marked.ToString(System.Globalization.CultureInfo.InvariantCulture),
        FactGameKind.TrueOrFalse => Marked == 1 ? True : False,
        _ => AuthorId,
    };
}

public sealed record FactVote(string VoterId, string Choice, bool Right);

/// <param name="RightGuesses">Votes that found the answer.</param>
/// <param name="Fooled">Players who voted wrong on this player's facts.</param>
public sealed record FactScore(string PlayerId, int Points, int RightGuesses, int Fooled);

/// <summary>The item on screen as one player may see it.</summary>
/// <param name="AuthorId">Null while it is a secret (Whose fact, before the reveal).</param>
/// <param name="Answer">The answer once revealed: a position or an author id.</param>
/// <param name="VotedIds">Who has voted, never what.</param>
/// <param name="Votes">Every vote, after the reveal.</param>
public sealed record FactItemView(
    string? AuthorId, string? Prompt, ImmutableArray<string> Texts, string? Answer, bool YouWrote,
    ImmutableArray<string> VoterIds, ImmutableArray<string> VotedIds, string? MyVote, ImmutableArray<FactVote> Votes);

/// <summary>Whose fact, at the end: every fact with its author and how many found it.</summary>
public sealed record FactRecap(string AuthorId, string Text, string? Prompt, int RightGuesses);

/// <summary>Everything one player's screen needs; built per player, so it never gives away a secret.</summary>
/// <param name="MyPrompts">While writing: the prompts for this player's facts.</param>
/// <param name="WroteIds">Who has written their facts.</param>
/// <param name="Scores">When they can be shown: always in One truth, after each fact or at the end in Whose fact.</param>
public sealed record FactView(
    FactSettings Settings,
    FactStage Stage,
    ImmutableArray<string> MyPrompts,
    bool CanSwapPrompt,
    bool YouWrote,
    ImmutableArray<string> WroteIds,
    int ItemNumber,
    int ItemCount,
    FactItemView? Item,
    ImmutableArray<FactScore> Scores,
    ImmutableArray<FactRecap> Recap);

/// <summary>
/// The fact games: everyone writes facts about themselves (with a prompt to help), then the facts come up one item at a
/// time. Online everyone votes on their own phone and scores; on one phone (<c>votes: false</c>) the group just reads,
/// talks and reveals. Not thread-safe: an online room calls it under its lock.
/// </summary>
public sealed class FactGame
{
    public const int FactsInOneTruth = 3;
    public const int MaxFactsEach = 3;
    public const int MaxFactLength = 160;

    public static int MinPlayers(FactGameKind kind) => kind == FactGameKind.WhoseFact ? 3 : 2;

    private readonly Random random;
    private readonly ImmutableArray<Player> players;
    private readonly ImmutableArray<Card> prompts;
    private readonly HashSet<string> usedPrompts = [];
    private readonly Dictionary<string, ImmutableArray<string>> promptsFor = [];
    private readonly Dictionary<string, (ImmutableArray<string> Texts, int Marked)> written = [];
    private readonly List<FactItem> items = [];
    private readonly List<Dictionary<string, string>> votes = [];
    private ImmutableArray<string> playing = [];

    public FactSettings Settings { get; }
    /// <summary>False on one phone: nobody votes, the group talks and then reveals.</summary>
    public bool Votes { get; }
    public FactStage Stage { get; private set; } = FactStage.Writing;
    public int Index { get; private set; }
    public IReadOnlyList<FactItem> Items => items;
    public ImmutableArray<Player> Players => players;

    public FactGame(IEnumerable<Player> players, FactSettings settings, IEnumerable<Card> prompts, int seed, bool votes = true)
    {
        this.players = [.. players];
        Settings = settings with
        {
            FactsEach = settings.Kind == FactGameKind.OneTruth ? 1 : Math.Clamp(settings.FactsEach, 1, MaxFactsEach),
            RevealEachFact = settings.Kind != FactGameKind.WhoseFact || settings.RevealEachFact,
            FindTheLie = settings.Kind == FactGameKind.OneTruth && settings.FindTheLie,
        };
        Votes = votes;
        random = new Random(seed);
        this.prompts = [.. prompts.DistinctBy(c => c.Id).OrderBy(_ => random.Next())];
        if (this.players.Length < MinPlayers(settings.Kind))
            throw new FactRuleException($"At least {MinPlayers(settings.Kind)} players are needed.");

        // Whose fact: everyone writes to the same prompts, so the facts are alike and harder to place.
        if (Settings.Kind == FactGameKind.WhoseFact)
        {
            var shared = DrawPrompts(Settings.FactsEach);
            foreach (var p in this.players) promptsFor[p.Id] = shared;
        }
        else
        {
            foreach (var p in this.players) promptsFor[p.Id] = DrawPrompts(Settings.FactsEach);
        }
    }

    private ImmutableArray<string> DrawPrompts(int count)
    {
        var fresh = prompts.Where(c => !usedPrompts.Contains(c.Id)).Take(count).ToList();
        // A small deck runs out: start over rather than leave someone without a prompt.
        if (fresh.Count < count) fresh.AddRange(prompts.Where(c => !fresh.Contains(c)).Take(count - fresh.Count));
        foreach (var card in fresh) usedPrompts.Add(card.Id);
        return [.. fresh.Select(c => c.Id)];
    }

    public ImmutableArray<string> PromptsFor(string playerId) => promptsFor.GetValueOrDefault(playerId, []);

    public string? PromptText(string? id, Lang lang) =>
        id is null ? null : prompts.FirstOrDefault(c => c.Id == id)?.TextFor(lang).Text;

    public bool HasWritten(string playerId) => written.ContainsKey(playerId);

    public ImmutableArray<string> WrittenIds => [.. players.Where(p => written.ContainsKey(p.Id)).Select(p => p.Id)];

    /// <summary>Other prompts for this player, while they have not written yet (not in Whose fact, where everyone shares them).</summary>
    public void SwapPrompt(string playerId)
    {
        RequireStage(FactStage.Writing);
        if (Settings.Kind == FactGameKind.WhoseFact) throw new FactRuleException("Everyone writes to the same prompts.");
        if (written.ContainsKey(playerId)) throw new FactRuleException("Your facts are already in.");
        Require(playerId);
        promptsFor[playerId] = DrawPrompts(Settings.FactsEach);
    }

    /// <summary>
    /// A player's facts. One truth: three, with <paramref name="marked"/> the position of the true one (or the lie).
    /// True or false: <paramref name="marked"/> holds one bit per fact, set when that fact is true.
    /// </summary>
    public void Write(string playerId, IReadOnlyList<string> texts, int marked = 0)
    {
        RequireStage(FactStage.Writing);
        Require(playerId);
        var count = Settings.Kind == FactGameKind.OneTruth ? FactsInOneTruth : Settings.FactsEach;
        var clean = (texts ?? []).Select(t => (t ?? "").Trim()).ToImmutableArray();
        if (clean.Length != count || clean.Any(t => t.Length == 0)) throw new FactRuleException($"Write {count} facts.");
        if (clean.Any(t => t.Length > MaxFactLength)) throw new FactRuleException($"Keep each fact under {MaxFactLength} characters.");
        if (Settings.Kind == FactGameKind.OneTruth && marked is < 0 or >= FactsInOneTruth) throw new FactRuleException("Mark one fact.");
        if (Settings.Kind == FactGameKind.TrueOrFalse && (marked < 0 || marked >= 1 << count)) throw new FactRuleException("Mark each fact true or made up.");
        written[playerId] = (clean, Settings.Kind == FactGameKind.WhoseFact ? 0 : marked);
        // Once everyone is in, the game moves on by itself.
        if (written.Count == players.Length) Begin();
    }

    /// <summary>Starts showing facts with those who have written (the host does not have to wait for someone who left).</summary>
    public void Begin()
    {
        RequireStage(FactStage.Writing);
        if (written.Count < MinPlayers(Settings.Kind))
            throw new FactRuleException($"At least {MinPlayers(Settings.Kind)} players need to write their facts.");
        playing = [.. players.Where(p => written.ContainsKey(p.Id)).Select(p => p.Id)];

        if (Settings.Kind == FactGameKind.OneTruth)
        {
            foreach (var id in playing.OrderBy(_ => random.Next()))
            {
                var (texts, marked) = written[id];
                var order = Enumerable.Range(0, texts.Length).OrderBy(_ => random.Next()).ToList();
                items.Add(new FactItem(id, promptsFor[id].FirstOrDefault(), [.. order.Select(i => texts[i])], order.IndexOf(marked)));
            }
        }
        else
        {
            // One fact per item; True or false keeps whether each one is true.
            var all = playing
                .SelectMany(id => written[id].Texts.Select((text, i) => new FactItem(id, promptsFor[id].ElementAtOrDefault(i), [text],
                    Settings.Kind == FactGameKind.TrueOrFalse ? (written[id].Marked >> i) & 1 : 0)))
                .OrderBy(_ => random.Next())
                .ToList();
            items.AddRange(SpreadAuthors(all));
        }
        votes.AddRange(items.Select(_ => new Dictionary<string, string>()));
        Index = 0;
        Stage = FactStage.Showing;
    }

    /// <summary>Avoids one author's facts coming up back to back where another order allows it.</summary>
    private static List<FactItem> SpreadAuthors(List<FactItem> shuffled)
    {
        var result = new List<FactItem>();
        var left = new List<FactItem>(shuffled);
        while (left.Count > 0)
        {
            var next = left.FirstOrDefault(i => result.Count == 0 || i.AuthorId != result[^1].AuthorId) ?? left[0];
            result.Add(next);
            left.Remove(next);
        }
        return result;
    }

    public FactItem? Current => Stage is FactStage.Showing or FactStage.Revealed && Index < items.Count ? items[Index] : null;

    /// <summary>Who votes on the current item: everyone but its author, except in Whose fact, where the author bluffs.</summary>
    public ImmutableArray<string> VotersFor(FactItem item) =>
        Settings.Kind == FactGameKind.WhoseFact ? playing : [.. playing.Where(id => id != item.AuthorId)];

    /// <summary>
    /// A vote on the current item: a position (One truth) or a player id (Whose fact, never yourself). The author's vote
    /// in Whose fact is only a bluff and scores nothing. When every voter is in, the answer comes out.
    /// </summary>
    public void Vote(string playerId, string choice)
    {
        RequireStage(FactStage.Showing);
        if (!Votes) throw new FactRuleException("Nobody votes in this game.");
        var item = Current!;
        if (!VotersFor(item).Contains(playerId)) throw new FactRuleException("You do not vote on your own facts.");
        if (Settings.Kind == FactGameKind.OneTruth)
        {
            if (!int.TryParse(choice, out var position) || position is < 0 or >= FactsInOneTruth) throw new FactRuleException("Pick one of the facts.");
        }
        else if (Settings.Kind == FactGameKind.TrueOrFalse)
        {
            if (choice is not (FactItem.True or FactItem.False)) throw new FactRuleException("True or made up?");
        }
        else if (choice == playerId || !playing.Contains(choice))
        {
            throw new FactRuleException("Pick another player.");
        }
        votes[Index][playerId] = choice;
        if (VotersFor(item).All(votes[Index].ContainsKey)) Reveal();
    }

    /// <summary>
    /// Shows the answer (the host may do it before everyone has voted). Whose fact with authors kept for the end goes
    /// straight on to the next fact instead.
    /// </summary>
    public void Reveal()
    {
        RequireStage(FactStage.Showing);
        if (Settings.Kind == FactGameKind.WhoseFact && !Settings.RevealEachFact) Next();
        else Stage = FactStage.Revealed;
    }

    public void Next()
    {
        if (Stage is not (FactStage.Showing or FactStage.Revealed)) throw new FactRuleException("No fact is up.");
        // Keeping the authors for the end: moving on before the reveal is how the game goes.
        if (Stage == FactStage.Showing && !(Settings.Kind == FactGameKind.WhoseFact && !Settings.RevealEachFact) && Votes)
            throw new FactRuleException("Reveal the answer first.");
        Index++;
        Stage = Index < items.Count ? FactStage.Showing : FactStage.Ended;
    }

    public void End() => Stage = FactStage.Ended;

    public ImmutableArray<FactVote> VotesOn(int index)
    {
        if (index < 0 || index >= items.Count) return [];
        var item = items[index];
        var answer = item.Answer(Settings.Kind);
        return [.. votes[index].Where(v => v.Key != item.AuthorId).Select(v => new FactVote(v.Key, v.Value, v.Value == answer))
            .Concat(votes[index].Where(v => v.Key == item.AuthorId).Select(v => new FactVote(v.Key, v.Value, false)))];
    }

    /// <summary>
    /// Points so far, counting only items whose answer is out: a right vote is a point for the voter, and every wrong
    /// vote is a point for the author, so a well-hidden fact pays off too.
    /// </summary>
    public ImmutableArray<FactScore> Scores(bool includeHidden = false)
    {
        var right = playing.ToDictionary(id => id, _ => 0);
        var fooled = playing.ToDictionary(id => id, _ => 0);
        for (var i = 0; i < items.Count; i++)
        {
            var counted = Stage == FactStage.Ended || includeHidden || i < Index || (i == Index && Stage == FactStage.Revealed);
            if (!counted) continue;
            foreach (var vote in VotesOn(i).Where(v => v.VoterId != items[i].AuthorId))
            {
                if (vote.Right) right[vote.VoterId]++;
                else fooled[items[i].AuthorId]++;
            }
        }
        return [.. playing
            .Select(id => new FactScore(id, right[id] + fooled[id], right[id], fooled[id]))
            .OrderByDescending(s => s.Points)];
    }

    /// <summary>Whether the scores may be shown now: Whose fact with authors kept for the end shows them only then.</summary>
    public bool ScoresShown => Votes && (Stage == FactStage.Ended || Settings.RevealEachFact);

    public FactView ForPlayer(string playerId, Lang lang)
    {
        var item = Current;
        FactItemView? itemView = null;
        if (item is not null)
        {
            var revealed = Stage == FactStage.Revealed;
            // One truth and True or false are about a named player; Whose fact keeps the author secret until the reveal.
            var authorShown = Settings.Kind != FactGameKind.WhoseFact || revealed;
            var mine = votes[Index];
            itemView = new FactItemView(
                authorShown ? item.AuthorId : null,
                PromptText(item.PromptId, lang),
                item.Texts,
                revealed ? item.Answer(Settings.Kind) : null,
                item.AuthorId == playerId,
                VotersFor(item),
                [.. mine.Keys],
                mine.GetValueOrDefault(playerId),
                revealed ? VotesOn(Index) : []);
        }

        var recap = Stage == FactStage.Ended && Settings.Kind == FactGameKind.WhoseFact
            // A game ended early lists only the facts that came up.
            ? items.Select((it, i) => (it, i)).Where(x => x.i < Index || votes[x.i].Count > 0)
                .Select(x => new FactRecap(x.it.AuthorId, x.it.Texts[0], PromptText(x.it.PromptId, lang), VotesOn(x.i).Count(v => v.Right))).ToImmutableArray()
            : [];

        return new FactView(
            Settings,
            Stage,
            Stage == FactStage.Writing ? [.. PromptsFor(playerId).Select(id => PromptText(id, lang) ?? "")] : [],
            Stage == FactStage.Writing && Settings.Kind != FactGameKind.WhoseFact && !written.ContainsKey(playerId) && prompts.Length > Settings.FactsEach,
            written.ContainsKey(playerId),
            WrittenIds,
            Math.Min(Index + 1, items.Count),
            items.Count,
            itemView,
            ScoresShown ? Scores() : [],
            recap);
    }

    private void RequireStage(FactStage stage)
    {
        if (Stage != stage) throw new FactRuleException(stage switch
        {
            FactStage.Writing => "The facts are already in.",
            FactStage.Showing => "No fact is waiting for votes.",
            _ => "Not now.",
        });
    }

    private void Require(string playerId)
    {
        if (players.All(p => p.Id != playerId)) throw new FactRuleException("You are not in this game.");
    }
}
