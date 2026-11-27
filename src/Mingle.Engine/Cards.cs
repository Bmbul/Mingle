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

/// <summary>
/// Which game a card belongs to. Question games play only question cards; blitz and "Give it to…" have their own
/// cards and their own decks, and a deck never mixes styles.
/// </summary>
public enum DeckStyle { Questions, Blitz, Handoff, Facts, Guess, Odd, Match }

/// <param name="Style">The game the kind belongs to: normal games, mixes and fallbacks use only question kinds.</param>
/// <param name="FixedOptions">Two answers the kind always offers ("Cringe" / "Fine"), so its cards hold only a text.</param>
/// <param name="Extras">How many more texts a card of this kind holds besides its text, in its options: the different
/// question of a pair (1), the things to rank (4). Not choices: "would you rather" has its own two options.</param>
public sealed record CardKind(string Id, ResponseType Response, Audience Audience, DeckStyle Style = DeckStyle.Questions,
    ImmutableDictionary<Lang, ImmutableArray<string>>? FixedOptions = null, int Extras = 0)
{
    public bool IsQuestion => Style == DeckStyle.Questions;

    /// <summary>Cards of this kind carry their own two options (would you rather).</summary>
    public bool CardsHaveOptions => Response == ResponseType.Choice && FixedOptions is null;

    /// <summary>How many options every card of this kind must have: two choices, its extra texts, or none.</summary>
    public int OptionCount => CardsHaveOptions ? 2 : Extras;
}

public static class CardKinds
{
    public static readonly CardKind OpenQuestion = new("open", ResponseType.Free, Audience.FollowTurnStyle);
    public static readonly CardKind FinishSentence = new("finish", ResponseType.Free, Audience.FollowTurnStyle);
    public static readonly CardKind WouldYouRather = new("wyr", ResponseType.Choice, Audience.Everyone);
    public static readonly CardKind MostLikelyTo = new("likely", ResponseType.PlayerVote, Audience.Everyone);
    /// <summary>"Guess the answer", a game of its own: a question about the turn player by name ({@}); the others guess.</summary>
    public static readonly CardKind GuessMyAnswer = new("guess", ResponseType.Free, Audience.AboutTurnPlayer, DeckStyle.Guess);
    public static readonly CardKind RateIt = new("rate", ResponseType.Scale, Audience.FollowTurnStyle);
    /// <summary>A situation everyone judges: cringe, or totally fine? The two answers come with the kind.</summary>
    public static readonly CardKind CringeOrFine = new("cringe", ResponseType.Choice, Audience.Everyone, DeckStyle.Questions,
        new Dictionary<Lang, ImmutableArray<string>>
        {
            [Lang.En] = ["Cringe", "Fine"],
            [Lang.Hy] = ["Ամոթ է", "Նորմալ է"],
            [Lang.Ru] = ["Стрём", "Норм"],
        }.ToImmutableDictionary());
    /// <summary>A single word or short phrase; the player says the first word it brings to mind.</summary>
    public static readonly CardKind Association = new("word", ResponseType.Free, Audience.FollowTurnStyle, DeckStyle.Blitz);
    /// <summary>Blitz: "Name 3 Armenian dishes", the card holding the whole challenge.</summary>
    public static readonly CardKind NameThree = new("name3", ResponseType.Free, Audience.FollowTurnStyle, DeckStyle.Blitz);
    /// <summary>Blitz: a category ("Fruit"); the app draws a letter each time ("Fruit: K").</summary>
    public static readonly CardKind LetterCategory = new("letter", ResponseType.Free, Audience.FollowTurnStyle, DeckStyle.Blitz);
    /// <summary>"Give the phone to…": who it describes, written to follow those words ("the best cook here").</summary>
    public static readonly CardKind Handoff = new("handoff", ResponseType.PlayerVote, Audience.Everyone, DeckStyle.Handoff);
    /// <summary>The fact games: a prompt for a fact about yourself ("Something about your childhood").</summary>
    public static readonly CardKind FactPrompt = new("fact", ResponseType.Free, Audience.Everyone, DeckStyle.Facts);
    /// <summary>"Guess the answer", ranked: four things the turn player puts in order ("favourite first"); the others guess the order.</summary>
    public static readonly CardKind RankIt = new("rank", ResponseType.Free, Audience.AboutTurnPlayer, DeckStyle.Guess, Extras: 4);
    /// <summary>"Who got a different question?": the question most players get; its one option is the different question.</summary>
    public static readonly CardKind OddPair = new("odd", ResponseType.Free, Audience.Everyone, DeckStyle.Odd, Extras: 1);
    /// <summary>"Same wavelength": a prompt everyone answers, hoping to write what the others write ("A red fruit").</summary>
    public static readonly CardKind MatchPrompt = new("match", ResponseType.Free, Audience.Everyone, DeckStyle.Match);

    public static ImmutableArray<CardKind> All { get; } =
        [OpenQuestion, FinishSentence, WouldYouRather, MostLikelyTo, GuessMyAnswer, RateIt, CringeOrFine,
         Association, NameThree, LetterCategory, Handoff, FactPrompt, RankIt, OddPair, MatchPrompt];

    /// <summary>The kinds normal games play.</summary>
    public static ImmutableArray<CardKind> Regular { get; } = [.. All.Where(k => k.IsQuestion)];

    /// <summary>The kinds blitz plays: quick, one-word answers. Blitz decks hold only these; question decks never do.</summary>
    public static ImmutableArray<CardKind> Blitz { get; } = [.. All.Where(k => k.Style == DeckStyle.Blitz)];

    /// <summary>The kinds of one style, e.g. what a card in a blitz deck may be.</summary>
    public static ImmutableArray<CardKind> OfStyle(DeckStyle style) => [.. All.Where(k => k.Style == style)];

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
    /// <param name="player">Who the card is about ("Guess my answer"): fills <c>{@}</c> in the text; without it a stand-in.</param>
    public CardText TextFor(Lang lang, string? player = null)
    {
        var text = Texts.TryGetValue(lang, out var own) ? own
            : Texts.TryGetValue(Lang.En, out var en) ? en
            : Texts.Values.First();
        if (PlayerSlot.In(text.Text))
            text = text with { Text = PlayerSlot.Fill(text.Text, lang, player ?? PlayerSlot.StandIn) };
        // A kind with fixed answers ("Cringe" / "Fine") supplies them in the reader's language.
        return Kind.FixedOptions is { } fixedOptions && text.Options.IsEmpty
            ? text with { Options = fixedOptions.GetValueOrDefault(lang, fixedOptions[Lang.En]) }
            : text;
    }
}

public sealed record Deck(string Id, ImmutableDictionary<Lang, string> Names, ImmutableArray<Card> Cards)
{
    public string NameFor(Lang lang) =>
        Names.TryGetValue(lang, out var name) ? name
        : Names.TryGetValue(Lang.En, out var en) ? en
        : Names.Values.FirstOrDefault() ?? Id;

    /// <summary>The game this deck is for, from its cards (an empty deck counts as questions).</summary>
    public DeckStyle Style => Cards.IsEmpty ? DeckStyle.Questions : Cards[0].Kind.Style;

    /// <summary>A blitz deck: its cards are all blitz cards.</summary>
    public bool IsBlitz => !Cards.IsEmpty && Style == DeckStyle.Blitz;

    /// <summary>True when one deck mixes cards of different games (blitz, questions, "Give it to…"), which is not allowed.</summary>
    public bool MixesStyles => Cards.Select(c => c.Kind.Style).Distinct().Skip(1).Any();

    /// <summary>How many cards everyone reading these languages can play (a card needs all of them).</summary>
    public int CountReadableBy(IReadOnlyCollection<Lang> languages) => CountReadableBy(languages, CardKinds.Regular);

    /// <summary>The same, counting only cards of the given kinds (for example the blitz kinds).</summary>
    public int CountReadableBy(IReadOnlyCollection<Lang> languages, IEnumerable<CardKind> kinds)
    {
        var ids = kinds.Select(k => k.Id).ToHashSet();
        return Cards.Count(c => ids.Contains(c.KindId) && languages.All(c.Supports));
    }
}

/// <summary>
/// Where a card names the player it is about: <c>{@}</c>, and in Armenian with the ending the sentence needs
/// (<c>{@ը}</c> subject, <c>{@ի}</c> "of", <c>{@ին}</c> "to", <c>{@ից}</c>, <c>{@ով}</c>), as in
/// «Ի՞նչ է {@ը} սովորաբար պատվիրում սրճարանում։» → «Ի՞նչ է Անին սովորաբար պատվիրում սրճարանում։».
/// "Guess my answer" cards are written this way, so the people guessing read a question about the player.
/// </summary>
public static class PlayerSlot
{
    /// <summary>Shown where no player is known (deck lists, the end of the game).</summary>
    public const string StandIn = "X";

    private static readonly System.Text.RegularExpressions.Regex Slot =
        new(@"\{@(ում|ին|ից|ով|ը|ն|ի)?\}", System.Text.RegularExpressions.RegexOptions.Compiled);

    public static bool In(string text) => text.Contains("{@", StringComparison.Ordinal);

    public static string Fill(string text, Lang lang, string name) =>
        Slot.Replace(text, m => lang == Lang.Hy
            ? ArmenianNames.WithEnding(name, m.Groups[1].Value, text[(m.Index + m.Length)..].TrimStart())
            : name);
}
