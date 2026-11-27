using System.Collections.Immutable;

namespace Mingle.Engine;

/// <summary>Answering: who has done it. Guessing (online): who do you think they are. Revealed: names and a storyteller.</summary>
public enum NeverStage { Answering, Guessing, Revealed, Ended }

/// <param name="Rounds">How many statements the game plays.</param>
/// <param name="Depths">The levels to play: 1 light, 2 personal, 3 bold; none means light and personal.</param>
public sealed record NeverSettings(int Rounds = 20, ImmutableArray<int> Depths = default)
{
    public ImmutableArray<int> Depths { get; init; } = Depths.IsDefaultOrEmpty ? [1, 2] : Depths;
}

/// <param name="Points">Online: a point for every player rightly guessed to have done it.</param>
/// <param name="Done">How many of the statements this player has done (for 🔥 and 😇 at the end).</param>
public sealed record NeverScore(string PlayerId, int Points, int Done);

/// <summary>Everything one player's screen needs; online nobody sees who has done it until the reveal.</summary>
/// <param name="HaveCount">Online, once everyone has answered: how many have done it.</param>
/// <param name="PicksNeeded">Online, while guessing: how many other players this player should pick.</param>
/// <param name="HaveIds">After the reveal: who has done it.</param>
/// <param name="Storyteller">After the reveal: who the app suggests tells the story.</param>
public sealed record NeverView(
    NeverStage Stage,
    int Number,
    int Count,
    int Depth,
    string? Statement,
    bool? MyAnswer,
    ImmutableArray<string> AnsweredIds,
    int HaveCount,
    int PicksNeeded,
    ImmutableArray<string> MyPicks,
    ImmutableArray<string> GuessedIds,
    ImmutableArray<string> HaveIds,
    string? Storyteller,
    ImmutableArray<NeverScore> Scores);

public sealed class NeverRuleException(string message) : Exception(message);

/// <summary>
/// «Ես երբեք…» / Never have I ever: a statement ("I have never fallen asleep in a cinema") and everyone who has done
/// it owns up. On one phone it is open (<c>secret: false</c>): the phone holder taps who has, and the app picks someone
/// to tell the story. Online everyone answers in secret, the screen shows only how many, and everyone guesses who, a
/// point for each right name. Not thread-safe: a room calls it under its lock.
/// </summary>
public sealed class NeverGame
{
    public const int MinPlayers = 2;

    private readonly ImmutableArray<Player> players;
    private readonly ImmutableArray<Card> cards;
    private readonly Random random;
    private readonly Dictionary<string, bool> answers = [];
    private readonly Dictionary<string, ImmutableArray<string>> picks = [];
    private readonly Dictionary<string, int> points;
    private readonly Dictionary<string, int> done;
    private string? storyteller;

    public bool Secret { get; }
    public NeverStage Stage { get; private set; } = NeverStage.Answering;
    public int Index { get; private set; }
    public int Count => cards.Length;
    public ImmutableArray<Player> Players => players;

    public NeverGame(IEnumerable<Player> players, IEnumerable<Card> pool, NeverSettings settings, int seed, bool secret = true)
    {
        this.players = [.. players];
        if (this.players.Length < MinPlayers) throw new NeverRuleException($"At least {MinPlayers} players are needed.");
        random = new Random(seed);
        var depths = settings.Depths;
        // Light first, bolder later, shuffled within each level.
        cards = [.. pool.Where(c => c.KindId == CardKinds.NeverHaveI.Id && depths.Contains(c.Depth)).DistinctBy(c => c.Id)
            .OrderBy(_ => random.Next()).Take(Math.Max(1, settings.Rounds)).OrderBy(c => c.Depth)];
        if (cards.IsEmpty) throw new NeverRuleException("The chosen decks have no statements at these levels.");
        Secret = secret;
        points = this.players.ToDictionary(p => p.Id, _ => 0);
        done = this.players.ToDictionary(p => p.Id, _ => 0);
    }

    public Card? Card => Stage == NeverStage.Ended ? null : cards[Index];

    public string? Statement(Lang lang) => Card?.TextFor(lang).Text;

    public bool? AnswerOf(string playerId) => answers.TryGetValue(playerId, out var has) ? has : null;

    public ImmutableArray<string> HaveIds => [.. players.Where(p => answers.GetValueOrDefault(p.Id)).Select(p => p.Id)];

    public string? Storyteller => storyteller;

    /// <summary>
    /// A player's answer. Online it is secret, and when everyone has answered the guessing starts; on one phone the
    /// phone holder taps each player who has (and can tap again to take it back).
    /// </summary>
    public void Answer(string playerId, bool has)
    {
        RequireStage(NeverStage.Answering);
        Require(playerId);
        answers[playerId] = has;
        if (Secret && players.All(p => answers.ContainsKey(p.Id))) StartGuessing();
    }

    /// <summary>How many other players this one should pick: everyone who has, minus themselves.</summary>
    public int PicksNeeded(string playerId) => HaveIds.Length - (answers.GetValueOrDefault(playerId) ? 1 : 0);

    /// <summary>Who still has to guess: nobody has something to guess when none or all of the others have done it.</summary>
    private ImmutableArray<string> Guessers =>
        [.. players.Where(p => PicksNeeded(p.Id) > 0 && PicksNeeded(p.Id) < players.Length - 1).Select(p => p.Id)];

    /// <summary>Online: on to the guessing (the host may start it without a missing answer).</summary>
    public void StartGuessing()
    {
        RequireStage(NeverStage.Answering);
        if (!Secret) { Reveal(); return; }
        Stage = NeverStage.Guessing;
        if (Guessers.IsEmpty) Reveal();
    }

    /// <summary>Online: the players this one thinks have done it. When every guesser is in, the names come out.</summary>
    public void Guess(string playerId, IReadOnlyCollection<string> ids)
    {
        RequireStage(NeverStage.Guessing);
        Require(playerId);
        var chosen = (ids ?? []).Distinct().ToImmutableArray();
        if (chosen.Contains(playerId) || chosen.Any(id => players.All(p => p.Id != id)))
            throw new NeverRuleException("Pick other players.");
        if (chosen.Length != PicksNeeded(playerId)) throw new NeverRuleException($"Pick {PicksNeeded(playerId)}.");
        picks[playerId] = chosen;
        if (Guessers.All(picks.ContainsKey)) Reveal();
    }

    /// <summary>The names come out (the host online may do it before every guess is in), with someone to tell the story.</summary>
    public void Reveal()
    {
        if (Stage is not (NeverStage.Answering or NeverStage.Guessing)) throw new NeverRuleException("Not now.");
        var have = HaveIds;
        foreach (var id in have) done[id]++;
        foreach (var (guesser, chosen) in picks)
            points[guesser] += chosen.Count(have.Contains);
        storyteller = have.IsEmpty ? null : have[random.Next(have.Length)];
        Stage = NeverStage.Revealed;
    }

    public void Next()
    {
        if (Stage == NeverStage.Ended) throw new NeverRuleException("The game is over.");
        if (Stage != NeverStage.Revealed) Reveal();
        Index++;
        answers.Clear();
        picks.Clear();
        storyteller = null;
        Stage = Index < cards.Length ? NeverStage.Answering : NeverStage.Ended;
    }

    public void End() => Stage = NeverStage.Ended;

    public ImmutableArray<NeverScore> Scores =>
        [.. players.Select(p => new NeverScore(p.Id, points[p.Id], done[p.Id])).OrderByDescending(s => s.Points).ThenByDescending(s => s.Done)];

    public NeverView ForPlayer(string playerId, Lang lang)
    {
        var revealed = Stage == NeverStage.Revealed;
        var countKnown = Stage is NeverStage.Guessing or NeverStage.Revealed;
        return new NeverView(
            Stage,
            Math.Min(Index + 1, cards.Length),
            cards.Length,
            Card?.Depth ?? 0,
            Statement(lang),
            AnswerOf(playerId),
            [.. players.Where(p => answers.ContainsKey(p.Id)).Select(p => p.Id)],
            countKnown ? HaveIds.Length : 0,
            Stage == NeverStage.Guessing && Guessers.Contains(playerId) ? PicksNeeded(playerId) : 0,
            picks.GetValueOrDefault(playerId, []),
            [.. players.Where(p => picks.ContainsKey(p.Id)).Select(p => p.Id)],
            revealed || !Secret ? HaveIds : [],
            revealed ? storyteller : null,
            Scores);
    }

    private void RequireStage(NeverStage stage)
    {
        if (Stage != stage) throw new NeverRuleException(stage == NeverStage.Answering ? "The answers are already in." : "Not now.");
    }

    private void Require(string playerId)
    {
        if (players.All(p => p.Id != playerId)) throw new NeverRuleException("You are not in this game.");
    }
}
