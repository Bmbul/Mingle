using System.Collections.Immutable;
using Mingle.Engine;

namespace Mingle.API.Ai;

/// <summary>
/// What to write about, in which languages. PlayerCount 0 means unknown (deck editor).
/// Depths empty (or all three) mixes light to deep; Setting null means any group (see <see cref="Settings"/>).
/// </summary>
public sealed record CardRequest(
    ImmutableArray<TopicInfo> Topics,
    ImmutableArray<Lang> Languages,
    int PlayerCount,
    int Count = TopicDeal.CardCount,
    ImmutableArray<int> Depths = default,
    string? Setting = null,
    DeckStyle Style = DeckStyle.Questions,
    string? Group = null,
    ImmutableArray<string> Kinds = default)
{
    public const string ThemeCode = "theme";
    public const int MaxThemeLength = 200;
    public const int MaxTopics = 5;

    /// <summary>
    /// The same request asking for a few extra cards (a fifth more, 2 to 5), so that after the
    /// checks drop a broken card there are still enough to keep exactly <see cref="Count"/>.
    /// </summary>
    public CardRequest WithSpares() => this with { Count = Count + Math.Clamp(Count / 5, 2, 5) };

    /// <summary>
    /// Moods for "Give it to…" cards, which are about the people in the room rather than a topic. "bold" is only for
    /// groups where it fits (see <see cref="BoldSettings"/>).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Moods = new Dictionary<string, string>
    {
        ["flattering"] = "kind and flattering: who is best at something, whom you'd trust, admire or want around",
        ["funny"] = "playfully embarrassing habits and quirks, never mean",
        ["situations"] = "who you'd pick for an imagined situation: a desert island, a road trip, a heist, a reality show",
        ["bold"] = "bolder, cheeky or flirty (exes, crushes, secrets, wild nights), still never about looks or anything hurtful",
    };

    public static readonly string[] BoldSettings = ["friends", "party", "couple"];

    /// <summary>"Give it to…" cards for the deck editor: about the chosen moods, with an optional note about the group.</summary>
    public static CardRequest ForHandoffDeck(IEnumerable<string> moods, string? group, string? setting, IEnumerable<Lang>? languages, int count)
    {
        var langs = (languages ?? []).Distinct().Order().ToImmutableArray();
        return new(
            [.. moods.Select(m => new TopicInfo(m, ImmutableDictionary<Lang, string>.Empty.Add(Lang.En, m), Moods[m]))],
            langs.IsEmpty ? [.. Langs.All] : langs, PlayerCount: 0, count, [],
            setting is not null && Settings.ContainsKey(setting) ? setting : null,
            DeckStyle.Handoff,
            string.IsNullOrWhiteSpace(group) ? null : group.Trim());
    }

    /// <summary>Who will play the deck, and what that means for the cards.</summary>
    public static readonly IReadOnlyDictionary<string, string> Settings = new Dictionary<string, string>
    {
        ["couple"] = "a couple (may be long-distance); cards can be about the two of them",
        ["friends"] = "close friends",
        ["family"] = "family members of different ages; keep it warm, nothing about romance or sex",
        ["party"] = "a lively group at a party; quick, funny, easy to answer out loud",
        ["colleagues"] = "colleagues; friendly and open, never too private (no romance, money, health or family problems)",
    };

    /// <summary>
    /// Cards for the deck editor, in every language: about the chosen topics and, if given, the
    /// user's own words, which become one more topic so the same prompt, schema and checks apply.
    /// </summary>
    public static CardRequest ForDeck(
        IEnumerable<TopicInfo> topics, string? theme, IEnumerable<int>? depths, string? setting, IEnumerable<Lang>? languages, int count,
        DeckStyle style = DeckStyle.Questions, IEnumerable<string>? kinds = null)
    {
        var all = topics.ToList();
        if (!string.IsNullOrWhiteSpace(theme))
            all.Add(new TopicInfo(ThemeCode, ImmutableDictionary<Lang, string>.Empty.Add(Lang.En, theme.Trim())));
        var langs = (languages ?? []).Distinct().Order().ToImmutableArray();
        var chosen = (depths ?? []).Where(d => d is >= 1 and <= 3).Distinct().Order().ToImmutableArray();
        return new([.. all], langs.IsEmpty ? [.. Langs.All] : langs, PlayerCount: 0, count,
            chosen.Length is 0 or 3 ? [] : chosen,
            setting is not null && Settings.ContainsKey(setting) ? setting : null,
            style,
            Kinds: style switch
            {
                DeckStyle.Questions => [.. (kinds ?? []).Where(CardPrompt.Kinds.Contains).Distinct()],
                DeckStyle.Blitz => [.. (kinds ?? []).Where(CardPrompt.BlitzKinds.Contains).Distinct()],
                _ => [],
            });
    }

    /// <summary>The card kinds this request may produce: the normal kinds, or the one kind of a blitz or "Give it to…" deck.</summary>
    public string[] AllowedKinds => Style switch
    {
        DeckStyle.Blitz => Kinds.IsDefaultOrEmpty ? CardPrompt.BlitzKinds : [.. Kinds],
        DeckStyle.Handoff => [CardKinds.Handoff.Id],
        DeckStyle.Facts => [CardKinds.FactPrompt.Id],
        DeckStyle.Guess => Kinds.IsDefaultOrEmpty ? [CardKinds.GuessMyAnswer.Id, CardKinds.RankIt.Id] : [.. Kinds],
        DeckStyle.Odd => [CardKinds.OddPair.Id],
        DeckStyle.Match => [CardKinds.MatchPrompt.Id],
        _ => Kinds.IsDefaultOrEmpty ? CardPrompt.Kinds : [.. Kinds],
    };

    /// <summary>True when the user picked only some of the question kinds.</summary>
    public bool ChosenKinds => Style == DeckStyle.Questions && !Kinds.IsDefaultOrEmpty && Kinds.Length < CardPrompt.Kinds.Length;

    /// <summary>Whether a card of this depth fits the request (any depth when no depths were chosen).</summary>
    public bool Allows(int depth) => Depths.IsDefaultOrEmpty || Depths.Contains(depth);
}

/// <summary>Writes new cards about the picked topics.</summary>
public interface ICardWriter
{
    /// <summary>False when no AI is configured; callers then use library cards.</summary>
    bool IsAvailable { get; }

    /// <summary>Valid cards only; throws <see cref="CardWriterException"/> when none could be written.</summary>
    Task<ImmutableArray<Card>> WriteAsync(CardRequest request, CancellationToken ct);
}

public sealed class CardWriterException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Used when no API key is set.</summary>
public sealed class NoCardWriter : ICardWriter
{
    public bool IsAvailable => false;

    public Task<ImmutableArray<Card>> WriteAsync(CardRequest request, CancellationToken ct) =>
        throw new CardWriterException("No AI is configured.");
}
