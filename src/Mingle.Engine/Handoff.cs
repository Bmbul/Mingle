using System.Collections.Immutable;

namespace Mingle.Engine;

/// <summary>One pass of the phone: who gave it to whom, for which card, and how the reveal went.</summary>
/// <param name="GuessedRight">Null when guessing was off.</param>
/// <param name="Revealed">False when the coin kept the reason secret (it still comes out at the end).</param>
public sealed record Handoff(string From, string To, string CardId, bool? GuessedRight, bool Revealed);

/// <summary>
/// "Give it to…": the player holding the phone secretly reads "Give the phone to the person who…" and hands it over
/// without a word. The receiver can guess why (the real card among decoys), a coin can keep the reason secret, and
/// then the receiver reads the next card. At the end the trail shows who got the phone and why. Pure rules here.
/// </summary>
public static class Handoffs
{
    public const int MinPlayers = 3;
    public const int Choices = 3;

    /// <summary>The "Give it to…" cards everyone at the table can read, each once.</summary>
    public static ImmutableArray<Card> Pool(IEnumerable<Deck> decks, IReadOnlyCollection<Lang> languages) =>
        [.. decks
            .SelectMany(d => d.Cards)
            .DistinctBy(c => c.Id)
            .Where(c => c.Kind.Style == DeckStyle.Handoff && languages.All(c.Supports))
            .OrderBy(c => c.Id, StringComparer.Ordinal)];

    /// <summary>The real card and up to two decoys from the same pool, in random order.</summary>
    public static ImmutableArray<Card> GuessChoices(Card answer, ImmutableArray<Card> pool, Random random)
    {
        var decoys = pool.Where(c => c.Id != answer.Id).OrderBy(_ => random.Next()).Take(Choices - 1);
        return [.. decoys.Append(answer).OrderBy(_ => random.Next())];
    }

    /// <summary>
    /// The coin: true shows the reason, false keeps it secret. A plain 50/50 coin can easily show the reason five times
    /// in a row, which feels broken in a game, so a streak gets less likely the longer it runs (50%, 35%, 20%) and never
    /// goes past four. Both sides are treated alike, so over a game it stays even.
    /// </summary>
    /// <param name="earlier">The earlier tosses of this game, oldest first.</param>
    public static bool CoinShows(IReadOnlyList<bool> earlier, Random random)
    {
        if (earlier.Count == 0) return random.Next(2) == 0;
        var last = earlier[^1];
        var streak = earlier.Reverse().TakeWhile(t => t == last).Count();
        var keepGoing = streak switch { 1 => 0.5, 2 => 0.35, 3 => 0.2, _ => 0.0 };
        return random.NextDouble() < keepGoing ? last : !last;
    }

    /// <summary>
    /// Who got the phone and why, most passes first (ties keep the playing order), with each player's right guesses.
    /// </summary>
    public static ImmutableArray<(Player Player, ImmutableArray<Handoff> Received, int RightGuesses)> Trail(
        IReadOnlyList<Player> players, IReadOnlyList<Handoff> passes) =>
        [.. players
            .Select((p, i) => (Player: p, Index: i,
                Received: passes.Where(h => h.To == p.Id).ToImmutableArray(),
                Right: passes.Count(h => h.To == p.Id && h.GuessedRight == true)))
            .OrderByDescending(x => x.Received.Length).ThenBy(x => x.Index)
            .Select(x => (x.Player, x.Received, x.Right))];
}
