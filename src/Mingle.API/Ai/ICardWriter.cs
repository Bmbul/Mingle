using System.Collections.Immutable;
using Mingle.Engine;

namespace Mingle.API.Ai;

/// <summary>
/// What to write about, in which languages. PlayerCount 0 means unknown (deck editor).
/// Depth null mixes light to deep; Setting null means any group (see <see cref="Settings"/>).
/// </summary>
public sealed record CardRequest(
    ImmutableArray<TopicInfo> Topics,
    ImmutableArray<Lang> Languages,
    int PlayerCount,
    int Count = TopicDeal.CardCount,
    int? Depth = null,
    string? Setting = null)
{
    public const string ThemeCode = "theme";
    public const int MaxThemeLength = 200;
    public const int MaxTopics = 5;

    /// <summary>
    /// The same request asking for a few extra cards (a fifth more, 2 to 5), so that after the
    /// checks drop a broken card there are still enough to keep exactly <see cref="Count"/>.
    /// </summary>
    public CardRequest WithSpares() => this with { Count = Count + Math.Clamp(Count / 5, 2, 5) };

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
    public static CardRequest ForDeck(IEnumerable<TopicInfo> topics, string? theme, int? depth, string? setting, int count)
    {
        var all = topics.ToList();
        if (!string.IsNullOrWhiteSpace(theme))
            all.Add(new TopicInfo(ThemeCode, ImmutableDictionary<Lang, string>.Empty.Add(Lang.En, theme.Trim())));
        return new([.. all], [.. Langs.All], PlayerCount: 0, count,
            depth is >= 1 and <= 3 ? depth : null,
            setting is not null && Settings.ContainsKey(setting) ? setting : null);
    }
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
