using System.Collections.Immutable;
using Mingle.Engine;

namespace Mingle.API.Ai;

/// <summary>What the table picked, in the languages the table reads. PlayerCount 0 means unknown (deck editor).</summary>
public sealed record CardRequest(ImmutableArray<TopicInfo> Topics, ImmutableArray<Lang> Languages, int PlayerCount, int Count = TopicDeal.CardCount)
{
    public const string ThemeCode = "theme";
    public const int MaxThemeLength = 200;

    /// <summary>
    /// Cards for the deck editor: about whatever the user wrote, in every language, for any group.
    /// The user's words become the one topic, so the same prompt, schema and checks apply.
    /// </summary>
    public static CardRequest ForDeck(string theme, int count) => new(
        [new TopicInfo(ThemeCode, ImmutableDictionary<Lang, string>.Empty.Add(Lang.En, theme.Trim()))],
        [.. Langs.All],
        PlayerCount: 0,
        count);
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
