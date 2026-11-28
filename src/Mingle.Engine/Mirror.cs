using System.Collections.Immutable;

namespace Mingle.Engine;

public enum MirrorStage { Placing, Revealed, Ended }

/// <param name="Laps">How many times each player is the one on the scale.</param>
public sealed record MirrorSettings(int Laps = 2);

/// <param name="Points">Points for placing the others close to where they placed themselves, plus the bonus for
/// knowing yourself.</param>
/// <param name="Bullseyes">Times this player put someone exactly where they put themselves.</param>
/// <param name="Gap">As the one on the scale: how far, on average, the group's placement was from their own
/// (null until someone has placed them).</param>
public sealed record MirrorScore(string PlayerId, int Points, int Bullseyes, double? Gap);

/// <summary>Where one player put the one on the scale, and the points it earned.</summary>
public sealed record MirrorPlacement(string PlayerId, int Value, int Points);

/// <param name="Self">Where the player on the scale put themselves.</param>
/// <param name="Average">Where the others put them, on average.</param>
/// <param name="SelfPoints">The bonus the player on the scale got for knowing themselves.</param>
public sealed record MirrorReveal(int Self, double Average, int SelfPoints, ImmutableArray<MirrorPlacement> Placements)
{
    /// <summary>How far the group's view is from the player's own: positive means the group put them further right.</summary>
    public double Gap => Average - Self;
}

/// <summary>A round worth talking about: the group saw someone very differently from how they see themselves.</summary>
public sealed record MirrorMoment(string SubjectId, string Left, string Right, int Self, double Average);

public sealed record MirrorView(
    MirrorStage Stage,
    int Round,
    int Rounds,
    string? SubjectId,
    string? Left,
    string? Right,
    int? MyValue,
    ImmutableArray<string> PlacedIds,
    MirrorReveal? Reveal,
    ImmutableArray<MirrorScore> Scores,
    ImmutableArray<MirrorMoment> Moments);

public sealed class MirrorRuleException(string message) : Exception(message);

/// <summary>
/// "How you see me", online: each round one player is on a scale ("Morning person ↔ Night owl"). They secretly
/// place themselves from 1 to 10, and everyone else places them too. The reveal shows both ("Ani says 2, you all
/// said 8"). The others score for placing them close to where they placed themselves (3 for exact, 2 for one off,
/// 1 for two off), and the one on the scale scores 2 when the group's average is within one of their own.
/// Not thread-safe: a room calls it under its lock.
/// </summary>
public sealed class MirrorGame
{
    public const int MinPlayers = 2;
    public const int Low = 1;
    public const int High = 10;
    public const int SelfBonus = 2;

    private sealed record Round(int Card, string SubjectId, int Self, double Average, int SelfPoints, ImmutableArray<MirrorPlacement> Placements);

    private readonly ImmutableArray<Player> players;
    /// <summary>The order players take the scale in.</summary>
    private readonly ImmutableArray<Player> order;
    private readonly ImmutableArray<Card> cards;
    private readonly Dictionary<string, int> values = [];
    private readonly List<Round> rounds = [];

    public MirrorStage Stage { get; private set; } = MirrorStage.Placing;
    public int Index { get; private set; }
    public int Count => cards.Length;

    public MirrorGame(IEnumerable<Player> players, IEnumerable<Card> pool, MirrorSettings settings, int seed)
    {
        this.players = [.. players];
        if (this.players.Length < MinPlayers) throw new MirrorRuleException($"At least {MinPlayers} players are needed.");
        var random = new Random(seed);
        order = [.. this.players.OrderBy(_ => random.Next())];
        var wanted = this.players.Length * Math.Clamp(settings.Laps, 1, 5);
        cards = [.. pool.Where(c => c.KindId == CardKinds.MirrorScale.Id && c.Texts.Values.All(t => t.Options.Length >= 1))
            .DistinctBy(c => c.Id).OrderBy(_ => random.Next()).Take(wanted)];
        if (cards.IsEmpty) throw new MirrorRuleException("The chosen decks have no scales for this game.");
    }

    /// <summary>Who is on the scale this round.</summary>
    public string? SubjectId => Stage == MirrorStage.Ended ? null : order[Index % order.Length].Id;

    public void Place(string playerId, int value)
    {
        RequireStage(MirrorStage.Placing);
        if (players.All(p => p.Id != playerId)) throw new MirrorRuleException("You are not in this game.");
        if (value is < Low or > High) throw new MirrorRuleException($"Pick a number from {Low} to {High}.");
        values[playerId] = value;
        if (players.All(p => values.ContainsKey(p.Id))) Reveal();
    }

    /// <summary>The placements come out; the host may do it before everyone has placed, once the one on the scale has.</summary>
    public void Reveal()
    {
        RequireStage(MirrorStage.Placing);
        var subject = SubjectId!;
        if (!values.TryGetValue(subject, out var self)) throw new MirrorRuleException("The player on the scale has not placed themselves yet.");
        var others = players.Where(p => p.Id != subject && values.ContainsKey(p.Id)).ToList();
        if (others.Count == 0) throw new MirrorRuleException("Nobody else has placed them yet.");
        var placements = others.Select(p => new MirrorPlacement(p.Id, values[p.Id], PointsFor(Math.Abs(values[p.Id] - self)))).ToImmutableArray();
        var average = placements.Average(p => p.Value);
        var selfPoints = Math.Abs(average - self) <= 1 ? SelfBonus : 0;
        rounds.Add(new Round(Index, subject, self, average, selfPoints, [.. placements.OrderBy(p => p.Value)]));
        Stage = MirrorStage.Revealed;
    }

    /// <summary>3 for exactly where they put themselves, 2 for one off, 1 for two off.</summary>
    public static int PointsFor(int distance) => distance switch { 0 => 3, 1 => 2, 2 => 1, _ => 0 };

    public void Next()
    {
        if (Stage == MirrorStage.Ended) throw new MirrorRuleException("The game is over.");
        if (Stage == MirrorStage.Placing) Reveal();
        Index++;
        values.Clear();
        Stage = Index < cards.Length ? MirrorStage.Placing : MirrorStage.Ended;
    }

    public void End() => Stage = MirrorStage.Ended;

    public ImmutableArray<MirrorScore> Scores =>
        [.. players.Select(p =>
        {
            var given = rounds.SelectMany(r => r.Placements).Where(x => x.PlayerId == p.Id).ToList();
            var own = rounds.Where(r => r.SubjectId == p.Id).ToList();
            return new MirrorScore(p.Id,
                given.Sum(x => x.Points) + own.Sum(r => r.SelfPoints),
                given.Count(x => x.Points == PointsFor(0)),
                own.Count == 0 ? null : own.Average(r => Math.Abs(r.Average - r.Self)));
        }).OrderByDescending(s => s.Points).ThenByDescending(s => s.Bullseyes)];

    private static string Left(Card card, Lang lang) => card.TextFor(lang).Text;
    private static string Right(Card card, Lang lang) => card.TextFor(lang).Options[0];

    /// <summary>The rounds where the group's view was furthest from the player's own, biggest first.</summary>
    public ImmutableArray<MirrorMoment> Moments(Lang lang, int take = 3) =>
        [.. rounds.Where(r => Math.Abs(r.Average - r.Self) >= 2).OrderByDescending(r => Math.Abs(r.Average - r.Self)).Take(take)
            .Select(r => new MirrorMoment(r.SubjectId, Left(cards[r.Card], lang), Right(cards[r.Card], lang), r.Self, r.Average))];

    public MirrorView ForPlayer(string playerId, Lang lang)
    {
        var card = Stage == MirrorStage.Ended ? null : cards[Index];
        var current = Stage == MirrorStage.Revealed ? rounds[^1] : null;
        return new(
            Stage,
            Math.Min(Index + 1, cards.Length),
            cards.Length,
            SubjectId,
            card is null ? null : Left(card, lang),
            card is null ? null : Right(card, lang),
            values.TryGetValue(playerId, out var mine) ? mine : null,
            [.. players.Where(p => values.ContainsKey(p.Id)).Select(p => p.Id)],
            current is null ? null : new MirrorReveal(current.Self, current.Average, current.SelfPoints, current.Placements),
            rounds.Count == 0 ? [] : Scores,
            Stage == MirrorStage.Ended ? Moments(lang) : []);
    }

    private void RequireStage(MirrorStage stage)
    {
        if (Stage != stage) throw new MirrorRuleException(stage == MirrorStage.Placing ? "The placements are already out." : "Not now.");
    }
}
