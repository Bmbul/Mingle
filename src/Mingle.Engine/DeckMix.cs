using System.Collections.Immutable;

namespace Mingle.Engine;

/// <summary>
/// "Everyone's decks": each player brings one deck, and the game takes a few random cards from
/// each and shuffles them together. A deck brought by two players gives twice as many cards.
/// </summary>
public static class DeckMix
{
    public const int CardsPerPlayer = 10;
    public const string Id = "everyone";

    /// <summary>
    /// Builds one deck from the players' picks (one entry per player, so a deck may repeat).
    /// Only cards every language at the table can read are taken, and no card is taken twice.
    /// Deterministic for a given seed.
    /// </summary>
    public static Deck FromPicks(IEnumerable<Deck> picks, IReadOnlyCollection<Lang> languages, int seed, int perPlayer = CardsPerPlayer)
    {
        var random = new Random(seed);
        var taken = new List<Card>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var group in picks.GroupBy(d => d.Id, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var readable = group.First().Cards
                .Where(c => c.Kind.IsQuestion && languages.All(c.Supports) && !seen.Contains(c.Id))
                .DistinctBy(c => c.Id)
                .OrderBy(c => c.Id, StringComparer.Ordinal)
                .ToArray();
            random.Shuffle(readable);
            foreach (var card in readable.Take(perPlayer * group.Count()))
            {
                seen.Add(card.Id);
                taken.Add(card);
            }
        }

        return new Deck(Id, ImmutableDictionary<Lang, string>.Empty, [.. taken]);
    }

    /// <summary>
    /// Settings for a game of the mixed deck: every mixed card is played once, in shuffled order,
    /// whatever its type or depth.
    /// </summary>
    public static GameSettings SettingsFor(GameSettings chosen, Deck mix) => chosen with
    {
        KindIds = CardKinds.Regular.Select(k => k.Id).ToImmutableHashSet(),
        MinDepth = 1,
        MaxDepth = 3,
        CardCount = Math.Max(1, mix.Cards.Length),
        Order = CardOrder.Shuffled,
    };
}
