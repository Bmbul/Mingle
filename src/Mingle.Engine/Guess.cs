using System.Collections.Immutable;

namespace Mingle.Engine;

/// <summary>Asking: the card is up and guesses come in. Marking: the answer is out and the player taps who got it.</summary>
public enum GuessStage { Asking, Marking, Ended }

/// <param name="Rounds">How many cards the game plays (fewer if the decks hold fewer).</param>
public sealed record GuessSettings(int Rounds = 20);

/// <summary>Points so far: right guesses, and how many guessers missed on this player's cards.</summary>
public sealed record GuessScore(string PlayerId, int Points, int Missed);

/// <summary>A written answer, shown once the answer is out: the player's own first, then the guesses.</summary>
/// <param name="Order">"Rank it": the order given, as positions into the card's items.</param>
/// <param name="Matches">"Rank it": how many items the guess put in the player's place (a point each).</param>
public sealed record GuessAnswer(string PlayerId, string Text, bool IsTurnPlayer, ImmutableArray<int> Order = default, int Matches = 0);

/// <summary>Everything one player's screen needs; built per player, so nobody sees a guess before the reveal.</summary>
/// <param name="Prompt">The card, with the turn player's name filled in, in the viewer's language.</param>
/// <param name="WrittenIds">Who has written (online), never what.</param>
/// <param name="Answers">After the reveal (online): the player's answer and every guess.</param>
/// <param name="RightIds">Who the player has marked as right (or closest).</param>
public sealed record GuessView(
    GuessStage Stage,
    int Number,
    int Count,
    string? TurnPlayerId,
    string? Prompt,
    bool YourTurn,
    bool YouWrote,
    ImmutableArray<string> GuesserIds,
    ImmutableArray<string> WrittenIds,
    ImmutableArray<GuessAnswer> Answers,
    ImmutableArray<string> RightIds,
    ImmutableArray<GuessScore> Scores,
    ImmutableArray<string> Items = default);

/// <summary>"Guess the answer" breaks a rule (wrong stage, someone marking for another player…).</summary>
public sealed class GuessRuleException(string message) : Exception(message);

/// <summary>
/// "Guess the answer": the turn goes round; each card asks about the player whose turn it is ("How many countries has
/// Ani been to?"). Everyone else guesses, the player answers, then taps who got it right (or closest): a point each.
/// On one phone the guesses are said out loud (<c>written: false</c>); online everyone types, and the answers show
/// side by side. Not thread-safe: an online room calls it under its lock.
/// </summary>
public sealed class GuessGame
{
    public const int MinPlayers = 2;
    public const int MaxAnswerLength = 120;

    private readonly ImmutableArray<Player> players;
    private readonly ImmutableArray<Card> cards;
    private readonly Dictionary<string, string> written = [];
    private readonly HashSet<string> right = [];
    private readonly Dictionary<string, int> points;
    private readonly Dictionary<string, int> missed;

    /// <summary>False on one phone: guesses are said out loud and only the marks are kept.</summary>
    public bool Written { get; }
    public GuessStage Stage { get; private set; } = GuessStage.Asking;
    /// <summary>The card being played, from 0.</summary>
    public int Index { get; private set; }
    public int Count => cards.Length;
    public ImmutableArray<Player> Players => players;

    public GuessGame(IEnumerable<Player> players, IEnumerable<Card> pool, GuessSettings settings, int seed, bool written = true)
    {
        this.players = [.. players];
        if (this.players.Length < MinPlayers) throw new GuessRuleException($"At least {MinPlayers} players are needed.");
        var random = new Random(seed);
        cards = [.. pool.Where(c => c.KindId == CardKinds.GuessMyAnswer.Id || c.KindId == CardKinds.RankIt.Id).DistinctBy(c => c.Id)
            .OrderBy(_ => random.Next()).Take(Math.Max(1, settings.Rounds))];
        if (cards.IsEmpty) throw new GuessRuleException("The chosen decks have no cards for this game.");
        Written = written;
        points = this.players.ToDictionary(p => p.Id, _ => 0);
        missed = this.players.ToDictionary(p => p.Id, _ => 0);
    }

    public Card? Card => Stage == GuessStage.Ended ? null : cards[Index];

    /// <summary>"Rank it": the turn player puts the card's items in order and the others guess the order.</summary>
    public bool IsRank => Card?.KindId == CardKinds.RankIt.Id;

    /// <summary>Online "Rank it" scores itself: a point for every item in the same place as the player's order.</summary>
    private bool AutoScored => IsRank && Written;

    /// <summary>"Rank it" answers are orders written as positions: "2,0,3,1".</summary>
    public static ImmutableArray<int>? ParseOrder(string text, int count)
    {
        var parts = (text ?? "").Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != count) return null;
        var order = new List<int>();
        foreach (var part in parts)
            if (int.TryParse(part, out var n) && n >= 0 && n < count && !order.Contains(n)) order.Add(n);
            else return null;
        return [.. order];
    }

    private int ItemCount => Card?.TextFor(Lang.En).Options.Length ?? 0;

    private int Matches(string guesserId)
    {
        if (!written.TryGetValue(TurnPlayer.Id, out var real) || !written.TryGetValue(guesserId, out var guess)) return 0;
        var a = ParseOrder(real, ItemCount);
        var b = ParseOrder(guess, ItemCount);
        return a is { } x && b is { } y ? x.Zip(y).Count(p => p.First == p.Second) : 0;
    }

    /// <summary>The turn goes round in seat order.</summary>
    public Player TurnPlayer => players[Index % players.Length];

    public ImmutableArray<string> Guessers => [.. players.Where(p => p.Id != TurnPlayer.Id).Select(p => p.Id)];

    /// <summary>The card in a language, with the turn player's name in it.</summary>
    public string? Prompt(Lang lang) => Card?.TextFor(lang, TurnPlayer.Name).Text;

    /// <summary>Online: the player's answer or a guess. When everyone has written, the answers come out.</summary>
    public void Write(string playerId, string text)
    {
        RequireStage(GuessStage.Asking);
        if (!Written) throw new GuessRuleException("Guesses are said out loud in this game.");
        Require(playerId);
        var clean = (text ?? "").Trim();
        if (clean.Length == 0) throw new GuessRuleException("Write something.");
        if (IsRank && ParseOrder(clean, ItemCount) is null) throw new GuessRuleException("Put every item in order.");
        if (clean.Length > MaxAnswerLength) clean = clean[..MaxAnswerLength];
        written[playerId] = clean;
        if (players.All(p => written.ContainsKey(p.Id))) Reveal();
    }

    public bool HasWritten(string playerId) => written.ContainsKey(playerId);

    /// <summary>The answer is out; the player now marks who got it. Online the host may do this before everyone wrote.</summary>
    public void Reveal()
    {
        RequireStage(GuessStage.Asking);
        Stage = GuessStage.Marking;
    }

    /// <summary>Marks or unmarks a guesser as right (or closest). Only the turn player marks, or the host online.</summary>
    public void Mark(string guesserId, bool isRight)
    {
        RequireStage(GuessStage.Marking);
        if (AutoScored) throw new GuessRuleException("Ranked cards score themselves.");
        if (!Guessers.Contains(guesserId)) throw new GuessRuleException("Only the others' guesses can be right.");
        if (isRight) right.Add(guesserId);
        else right.Remove(guesserId);
    }

    /// <summary>Counts this card's points and moves on to the next card, with the next player's turn.</summary>
    public void Next()
    {
        if (Stage == GuessStage.Ended) throw new GuessRuleException("The game is over.");
        if (Stage == GuessStage.Asking) Reveal();
        Score();
        Index++;
        written.Clear();
        right.Clear();
        Stage = Index < cards.Length ? GuessStage.Asking : GuessStage.Ended;
    }

    /// <summary>Ends early; the marks on the card being played still count.</summary>
    public void End()
    {
        if (Stage == GuessStage.Ended) return;
        if (Stage == GuessStage.Marking) Score();
        Stage = GuessStage.Ended;
    }

    private void Score()
    {
        if (AutoScored)
        {
            foreach (var id in Guessers)
            {
                var matches = Matches(id);
                points[id] += matches;
                if (matches < ItemCount) missed[TurnPlayer.Id]++;
            }
            return;
        }
        foreach (var id in right) points[id]++;
        missed[TurnPlayer.Id] += Guessers.Length - right.Count;
    }

    public ImmutableArray<GuessScore> Scores =>
        [.. players.Select(p => new GuessScore(p.Id, points[p.Id], missed[p.Id])).OrderByDescending(s => s.Points)];

    public ImmutableArray<string> RightIds => [.. players.Where(p => right.Contains(p.Id)).Select(p => p.Id)];

    public GuessView ForPlayer(string? playerId, Lang lang)
    {
        var ended = Stage == GuessStage.Ended;
        var turn = ended ? null : TurnPlayer;
        var answers = Stage == GuessStage.Marking && Written
            ? players.Where(p => written.ContainsKey(p.Id))
                .Select(p => new GuessAnswer(p.Id, written[p.Id], p.Id == turn!.Id,
                    IsRank ? ParseOrder(written[p.Id], ItemCount) ?? [] : [],
                    IsRank && p.Id != turn.Id ? Matches(p.Id) : 0))
                .OrderByDescending(a => a.IsTurnPlayer)
                .ToImmutableArray()
            : [];
        return new GuessView(
            Stage,
            Math.Min(Index + 1, cards.Length),
            cards.Length,
            turn?.Id,
            Prompt(lang),
            turn is not null && turn.Id == playerId,
            playerId is not null && written.ContainsKey(playerId),
            ended ? [] : Guessers,
            [.. players.Where(p => written.ContainsKey(p.Id)).Select(p => p.Id)],
            answers,
            ended ? [] : RightIds,
            Scores,
            IsRank ? Card!.TextFor(lang).Options : []);
    }

    private void RequireStage(GuessStage stage)
    {
        if (Stage != stage) throw new GuessRuleException(stage == GuessStage.Asking ? "The answer is already out." : "Not now.");
    }

    private void Require(string playerId)
    {
        if (players.All(p => p.Id != playerId)) throw new GuessRuleException("You are not in this game.");
    }
}
