using System.Collections.Immutable;

namespace Mingle.Engine;

/// <summary>Languages the game ships with.</summary>
public enum Lang { En, Hy, Ru }

public static class Langs
{
    public static ImmutableArray<Lang> All { get; } = [Lang.Hy, Lang.Ru, Lang.En];

    public static string Code(this Lang lang) => lang switch
    {
        Lang.En => "en",
        Lang.Hy => "hy",
        Lang.Ru => "ru",
        _ => throw new ArgumentOutOfRangeException(nameof(lang), lang, null),
    };

    public static bool TryParse(string? code, out Lang lang)
    {
        switch (code?.Trim().ToLowerInvariant())
        {
            case "en": lang = Lang.En; return true;
            case "hy": lang = Lang.Hy; return true;
            case "ru": lang = Lang.Ru; return true;
            default: lang = default; return false;
        }
    }
}

/// <summary>What kind of response a card asks for, before the answer mode is applied.</summary>
public enum ResponseType
{
    /// <summary>A free answer: typed in written mode, spoken in talk mode.</summary>
    Free,
    /// <summary>Pick one of the card's options.</summary>
    Choice,
    /// <summary>Vote for one of the players.</summary>
    PlayerVote,
    /// <summary>A number from 1 to 10.</summary>
    Scale,
}

/// <summary>Who is asked to respond to a card.</summary>
public enum Audience
{
    /// <summary>Everyone, or only the turn player, depending on the game's turn style.</summary>
    FollowTurnStyle,
    /// <summary>Always everyone (votes and choices need the whole group).</summary>
    Everyone,
    /// <summary>The turn player answers about themselves; everyone else guesses their answer.</summary>
    AboutTurnPlayer,
}

/// <summary>The input a player actually uses for the current card, after the answer mode is applied.</summary>
public enum InputMode { Spoken, Text, Choice, PlayerVote, Scale }

/// <param name="BlitzOnly">Played only in blitz (associations): normal games, mixes and fallbacks leave it out.</param>
public sealed record CardKind(string Id, ResponseType Response, Audience Audience, bool BlitzOnly = false);

public static class CardKinds
{
    public static readonly CardKind OpenQuestion = new("open", ResponseType.Free, Audience.FollowTurnStyle);
    public static readonly CardKind FinishSentence = new("finish", ResponseType.Free, Audience.FollowTurnStyle);
    public static readonly CardKind WouldYouRather = new("wyr", ResponseType.Choice, Audience.Everyone);
    public static readonly CardKind MostLikelyTo = new("likely", ResponseType.PlayerVote, Audience.Everyone);
    public static readonly CardKind GuessMyAnswer = new("guess", ResponseType.Free, Audience.AboutTurnPlayer);
    public static readonly CardKind RateIt = new("rate", ResponseType.Scale, Audience.FollowTurnStyle);
    /// <summary>A single word or short phrase; the player says the first word it brings to mind.</summary>
    public static readonly CardKind Association = new("word", ResponseType.Free, Audience.FollowTurnStyle, BlitzOnly: true);

    public static ImmutableArray<CardKind> All { get; } =
        [OpenQuestion, FinishSentence, WouldYouRather, MostLikelyTo, GuessMyAnswer, RateIt, Association];

    /// <summary>The kinds normal games play.</summary>
    public static ImmutableArray<CardKind> Regular { get; } = [.. All.Where(k => !k.BlitzOnly)];

    /// <summary>The kinds blitz plays: quick, one-word answers. Blitz decks hold only these; question decks never do.</summary>
    public static ImmutableArray<CardKind> Blitz { get; } = [.. All.Where(k => k.BlitzOnly)];

    private static readonly ImmutableDictionary<string, CardKind> ById = All.ToImmutableDictionary(k => k.Id);

    public static bool TryGet(string id, out CardKind kind) => ById.TryGetValue(id, out kind!);

    public static CardKind Get(string id) =>
        ById.TryGetValue(id, out var kind) ? kind : throw new KeyNotFoundException($"Unknown card kind '{id}'.");
}

public sealed record CardText(string Text, ImmutableArray<string> Options)
{
    public CardText(string text) : this(text, []) { }
}

public sealed record Card(
    string Id,
    string KindId,
    int Depth,
    ImmutableArray<string> Topics,
    ImmutableDictionary<Lang, CardText> Texts)
{
    public CardKind Kind => CardKinds.Get(KindId);

    public bool Supports(Lang lang) => Texts.ContainsKey(lang);

    /// <summary>The card in the requested language, falling back to English, then to any language it has.</summary>
    public CardText TextFor(Lang lang) =>
        Texts.TryGetValue(lang, out var text) ? text
        : Texts.TryGetValue(Lang.En, out var en) ? en
        : Texts.Values.First();
}

public sealed record Deck(string Id, ImmutableDictionary<Lang, string> Names, ImmutableArray<Card> Cards)
{
    public string NameFor(Lang lang) =>
        Names.TryGetValue(lang, out var name) ? name
        : Names.TryGetValue(Lang.En, out var en) ? en
        : Names.Values.FirstOrDefault() ?? Id;

    /// <summary>A blitz deck: its cards are all blitz cards. A deck is either blitz or questions, never both.</summary>
    public bool IsBlitz => !Cards.IsEmpty && Cards.All(c => c.Kind.BlitzOnly);

    /// <summary>True when blitz cards and question cards are mixed in one deck, which is not allowed.</summary>
    public bool MixesBlitzAndQuestions => Cards.Any(c => c.Kind.BlitzOnly) && Cards.Any(c => !c.Kind.BlitzOnly);

    /// <summary>How many cards everyone reading these languages can play (a card needs all of them).</summary>
    public int CountReadableBy(IReadOnlyCollection<Lang> languages) => CountReadableBy(languages, CardKinds.Regular);

    /// <summary>The same, counting only cards of the given kinds (for example the blitz kinds).</summary>
    public int CountReadableBy(IReadOnlyCollection<Lang> languages, IEnumerable<CardKind> kinds)
    {
        var ids = kinds.Select(k => k.Id).ToHashSet();
        return Cards.Count(c => ids.Contains(c.KindId) && languages.All(c.Supports));
    }
}
