using System.Collections.Immutable;

namespace Mingle.Engine;

/// <summary>
/// Blitz: each player gets a short turn in the hot seat and answers as many quick cards as they can
/// (associations, "would you rather"), each card with its own short timer. The rules here are pure;
/// the clock lives in the client.
/// </summary>
public static class Blitz
{
    public const int TurnSeconds = 60;
    public const int CardSeconds = 5;

    /// <summary>The blitz cards one player can read, from the chosen decks, each card once.</summary>
    public static ImmutableArray<Card> Pool(IEnumerable<Deck> decks, Lang lang)
    {
        var kinds = CardKinds.Blitz.Select(k => k.Id).ToHashSet();
        return [.. decks
            .SelectMany(d => d.Cards)
            .DistinctBy(c => c.Id)
            .Where(c => kinds.Contains(c.KindId) && c.Supports(lang))
            .OrderBy(c => c.Id, StringComparer.Ordinal)];
    }

    /// <summary>
    /// The next card for a turn: a random card nobody has seen yet this game; once every card has been
    /// seen, the pile starts over. Returns null only when the pool is empty.
    /// </summary>
    public static Card? Next(ImmutableArray<Card> pool, ISet<string> seen, Random random)
    {
        if (pool.IsEmpty) return null;
        var fresh = pool.Where(c => !seen.Contains(c.Id)).ToList();
        if (fresh.Count == 0)
        {
            foreach (var card in pool) seen.Remove(card.Id);
            fresh = [.. pool];
        }
        var next = fresh[random.Next(fresh.Count)];
        seen.Add(next.Id);
        return next;
    }

    /// <summary>Players ordered by answers, most first; ties keep the order they played in.</summary>
    public static ImmutableArray<(Player Player, int Answered)> Ranking(IEnumerable<(Player Player, int Answered)> scores) =>
        [.. scores.Select((s, i) => (s, i)).OrderByDescending(x => x.s.Answered).ThenBy(x => x.i).Select(x => x.s)];
}
