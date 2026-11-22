using System.Collections.Immutable;

namespace Mingle.Engine;

/// <summary>A topic players can pick in the "Our topics" game, with its name in each language.</summary>
public sealed record TopicInfo(string Code, ImmutableDictionary<Lang, string> Names)
{
    public string NameFor(Lang lang) =>
        Names.TryGetValue(lang, out var name) ? name
        : Names.TryGetValue(Lang.En, out var en) ? en
        : Code;
}

/// <summary>
/// "Our topics": each player is offered a few random topics and picks up to three, anonymously.
/// The cards are then written about everything the table picked.
/// </summary>
public static class TopicDeal
{
    public const int OfferSize = 10;
    public const int MaxPicks = 3;
    public const int CardCount = 20;

    /// <summary>
    /// Deals an offer of random topic codes. Topics in <paramref name="avoid"/> (the previous offer,
    /// when asking for a new one) are only used if there are not enough others.
    /// </summary>
    public static ImmutableArray<string> Offer(IEnumerable<TopicInfo> pool, int seed, IReadOnlyCollection<string>? avoid = null, int size = OfferSize)
    {
        var random = new Random(seed);
        var codes = pool.Select(t => t.Code).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        random.Shuffle(codes);
        var fresh = codes.Where(c => avoid?.Contains(c) != true);
        var again = codes.Where(c => avoid?.Contains(c) == true);
        return [.. fresh.Concat(again).Take(size)];
    }

    /// <summary>
    /// Library cards for the picked topics, used when the AI cannot write cards (no key, error,
    /// daily limit). Cards on more of the picked topics come first; if too few match, other
    /// readable cards fill up the game. Deterministic for a given seed.
    /// </summary>
    public static Deck FromLibrary(IEnumerable<Deck> decks, IReadOnlyCollection<string> topics, IReadOnlyCollection<Lang> languages, int seed, int count = CardCount)
    {
        var random = new Random(seed);
        var readable = decks
            .SelectMany(d => d.Cards)
            .DistinctBy(c => c.Id)
            .Where(c => languages.All(c.Supports))
            .OrderBy(c => c.Id, StringComparer.Ordinal)
            .ToArray();
        random.Shuffle(readable);

        var picked = readable
            .OrderByDescending(c => c.Topics.Count(topics.Contains))
            .Take(count)
            .ToImmutableArray();
        return new Deck("topics", ImmutableDictionary<Lang, string>.Empty, picked);
    }

    /// <summary>
    /// Settings for a topics game: every written card is played once, light to deep, whatever
    /// type or depth filters were set.
    /// </summary>
    public static GameSettings SettingsFor(GameSettings chosen, Deck cards) => chosen with
    {
        KindIds = CardKinds.All.Select(k => k.Id).ToImmutableHashSet(),
        MinDepth = 1,
        MaxDepth = 3,
        CardCount = Math.Max(1, cards.Cards.Length),
    };
}
