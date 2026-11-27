using System.Collections.Immutable;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mingle.API.Ai;
using Mingle.DAL;
using Mingle.Engine;

namespace Mingle.API.Controllers;

/// <summary>
/// Decks in the deck file format. The starter list and any deck by id are public (a deck's id is
/// its share link); changing a deck made in the editor needs the edit key returned when it was
/// created, sent in the <c>X-Edit-Key</c> header.
/// </summary>
[ApiController]
[Route("api/decks")]
public sealed class DecksController(IDeckSource starterDecks, UserDeckStore userDecks, ITopicSource topicSource, ICardWriter writer, ILogger<DecksController> log) : ControllerBase
{
    public const string EditKeyHeader = "X-Edit-Key";
    public const string WritePolicy = "deck-writes";
    private const int MaxBodyBytes = 512 * 1024;
    private const int MaxNameLength = 60;
    private const string Json = "application/json; charset=utf-8";

    /// <summary>Per visitor: plenty for editing by hand, too little for filling the database.</summary>
    public static void AddWriteLimit(RateLimiterOptions options)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy(WritePolicy, http => RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(10) }));
    }

    /// <summary>The starter decks.</summary>
    [HttpGet]
    public async Task<IActionResult> GetStarter(CancellationToken ct) =>
        Content(DeckJson.WriteMany(await starterDecks.GetDecksAsync(ct)), Json);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        await userDecks.GetAsync(id, ct) is { } deck ? Content(DeckJson.Write(deck), Json) : NotFound();

    /// <summary>Creates a deck; the answer holds its id and the edit key (shown only this once).</summary>
    [HttpPost]
    [EnableRateLimiting(WritePolicy)]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var (deck, error) = await ReadDeck(ct);
        if (deck is null) return BadRequest(new { error });
        var (id, key) = await userDecks.CreateAsync(deck, ct);
        return Created($"/api/decks/{id}", new { id, editKey = key });
    }

    /// <param name="Topics">Topic codes from <c>GET /api/topics</c>, up to five.</param>
    /// <param name="Theme">The user's own words, optional when topics are given.</param>
    /// <param name="Depths">Any of 1, 2, 3; none or all three mixes light to deep.</param>
    /// <param name="Setting">Who plays: couple, friends, family, party or colleagues.</param>
    /// <param name="Languages">Language codes (hy, ru, en) to write the cards in; none means all three.</param>
    /// <param name="Style">"associations" for blitz cards, "handoff" for "Give it to…", "facts" for fact-game prompts; anything else writes normal cards.</param>
    /// <param name="Moods">"Give it to…" only, instead of topics: flattering, funny, situations, bold.</param>
    /// <param name="Kinds">Questions only: the card kinds to write (open, finish, wyr, likely, guess, rate); none means all.</param>
    public sealed record WriteRequest(string[]? Topics, string? Theme, int[]? Depths, string? Setting, string[]? Languages, int Count = 20,
        string? Style = null, string[]? Moods = null, string[]? Kinds = null);

    /// <summary>
    /// Writes new cards about a theme for the deck editor, in every language. Nothing is saved: the
    /// cards come back as a deck file for the user to edit, keep or drop before saving the deck.
    /// </summary>
    [HttpPost("write")]
    [EnableRateLimiting(WritePolicy)]
    public async Task<IActionResult> Write(WriteRequest request, CancellationToken ct)
    {
        var theme = request.Theme?.Trim() ?? "";
        var codes = (request.Topics ?? []).Distinct(StringComparer.Ordinal).ToList();
        var style = request.Style?.ToLowerInvariant() switch
        {
            "associations" => DeckStyle.Blitz,
            "handoff" => DeckStyle.Handoff,
            "facts" => DeckStyle.Facts,
            "guess" => DeckStyle.Guess,
            _ => DeckStyle.Questions,
        };
        if (style == DeckStyle.Handoff) return await WriteHandoff(request, theme, ct);
        if (theme.Length == 0 && codes.Count == 0) return BadRequest(new { error = "Pick a topic or say what the cards should be about." });
        if (theme.Length > CardRequest.MaxThemeLength) return BadRequest(new { error = $"Keep it under {CardRequest.MaxThemeLength} characters." });
        if (codes.Count > CardRequest.MaxTopics) return BadRequest(new { error = $"Pick up to {CardRequest.MaxTopics} topics." });
        if (request.Setting is { } setting && !CardRequest.Settings.ContainsKey(setting)) return BadRequest(new { error = "Unknown setting." });
        if ((request.Depths ?? []).Any(d => d is < 1 or > 3)) return BadRequest(new { error = "Depth is 1, 2 or 3." });
        var validKinds = style switch { DeckStyle.Blitz => CardPrompt.BlitzKinds, DeckStyle.Facts or DeckStyle.Guess => [], _ => CardPrompt.Kinds };
        if ((request.Kinds ?? []).Any(k => !validKinds.Contains(k))) return BadRequest(new { error = "Unknown card kind." });
        var languages = new List<Lang>();
        foreach (var code in request.Languages ?? [])
        {
            if (!Langs.TryParse(code, out var lang)) return BadRequest(new { error = $"Unknown language '{code}'." });
            languages.Add(lang);
        }

        var known = (await topicSource.GetPickableTopicsAsync(ct)).Where(t => codes.Contains(t.Code)).ToList();
        if (known.Count != codes.Count) return BadRequest(new { error = "Unknown topic." });
        if (!writer.IsAvailable) return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Card generation is not available." });

        try
        {
            var cards = await writer.WriteAsync(
                CardRequest.ForDeck(known, theme, request.Depths, request.Setting, languages, Math.Clamp(request.Count, 1, 20), style, request.Kinds), ct);
            // Topics are dropped: the theme is the user's own words, not a library topic.
            // Named after the theme: the deck file format needs a name, and the client reads it with the same parser.
            var name = theme.Length > 0 ? theme : string.Join(", ", known.Select(t => t.NameFor(Lang.En)));
            var deck = new Deck("written", ImmutableDictionary<Lang, string>.Empty.Add(Lang.En, name), [.. cards.Select(c => c with { Topics = [] })]);
            return Content(DeckJson.Write(deck), Json);
        }
        catch (CardWriterException ex)
        {
            log.LogWarning("Writing deck cards failed: {Reason}", ex.Message);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [EnableRateLimiting(WritePolicy)]
    public async Task<IActionResult> Save(Guid id, [FromHeader(Name = EditKeyHeader)] string? key, CancellationToken ct)
    {
        var (deck, error) = await ReadDeck(ct);
        if (deck is null) return BadRequest(new { error });
        return ToResult(await userDecks.SaveAsync(id, key, deck, ct));
    }

    [HttpDelete("{id:guid}")]
    [EnableRateLimiting(WritePolicy)]
    public async Task<IActionResult> Delete(Guid id, [FromHeader(Name = EditKeyHeader)] string? key, CancellationToken ct) =>
        ToResult(await userDecks.DeleteAsync(id, key, ct));

    private IActionResult ToResult(DeckChange change) => change switch
    {
        DeckChange.Done => NoContent(),
        DeckChange.NotFound => NotFound(),
        _ => StatusCode(StatusCodes.Status403Forbidden),
    };

    /// <summary>Reads and checks the body as a deck file; the message says what is wrong.</summary>
    /// <summary>"Give it to…" cards: moods instead of topics, and the user's words are a note about the group.</summary>
    private async Task<IActionResult> WriteHandoff(WriteRequest request, string group, CancellationToken ct)
    {
        var moods = (request.Moods ?? []).Distinct(StringComparer.Ordinal).ToList();
        if (moods.Count == 0) return BadRequest(new { error = "Pick a mood." });
        if (moods.Any(m => !CardRequest.Moods.ContainsKey(m))) return BadRequest(new { error = "Unknown mood." });
        if (moods.Contains("bold") && !CardRequest.BoldSettings.Contains(request.Setting))
            return BadRequest(new { error = "Bold cards are for friends, parties and couples." });
        if (group.Length > CardRequest.MaxThemeLength) return BadRequest(new { error = $"Keep it under {CardRequest.MaxThemeLength} characters." });
        if (request.Setting is { } setting && !CardRequest.Settings.ContainsKey(setting)) return BadRequest(new { error = "Unknown setting." });
        var languages = new List<Lang>();
        foreach (var code in request.Languages ?? [])
        {
            if (!Langs.TryParse(code, out var lang)) return BadRequest(new { error = $"Unknown language '{code}'." });
            languages.Add(lang);
        }
        if (!writer.IsAvailable) return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Card generation is not available." });

        try
        {
            var cards = await writer.WriteAsync(
                CardRequest.ForHandoffDeck(moods, group, request.Setting, languages, Math.Clamp(request.Count, 1, 20)), ct);
            var deck = new Deck("written", ImmutableDictionary<Lang, string>.Empty.Add(Lang.En, "Give it to…"), [.. cards.Select(c => c with { Topics = [] })]);
            return Content(DeckJson.Write(deck), Json);
        }
        catch (CardWriterException ex)
        {
            log.LogWarning("Writing Give it to… cards failed: {Reason}", ex.Message);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
    }

    private async Task<(Deck? Deck, string? Error)> ReadDeck(CancellationToken ct)
    {
        if (Request.ContentLength > MaxBodyBytes) return (null, "The deck is too large.");
        string json;
        using (var reader = new StreamReader(Request.Body))
            json = await reader.ReadToEndAsync(ct);
        if (json.Length > MaxBodyBytes) return (null, "The deck is too large.");

        Deck deck;
        try { deck = DeckJson.Parse(json); }
        catch (Exception ex) when (ex is FormatException or System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return (null, ex.Message);
        }
        return Check(deck) is { } problem ? (null, problem) : (deck, null);
    }

    public static string? Check(Deck deck)
    {
        if (deck.Names.Values.Any(n => n.Length > MaxNameLength)) return $"Deck names can be up to {MaxNameLength} characters.";
        if (deck.Cards.Length > UserDeckStore.MaxCards) return $"A deck can hold up to {UserDeckStore.MaxCards} cards.";
        if (deck.MixesStyles) return "A deck holds cards for one game only: questions, blitz, \"Give it to…\", facts or \"Guess the answer\".";
        foreach (var card in deck.Cards)
            foreach (var (lang, text) in card.Texts)
            {
                if (text.Text.Length > CardParser.MaxTextLength) return $"A card text in {lang.Code()} is too long.";
                if (text.Options.Any(o => o.Length > CardParser.MaxOptionLength)) return $"An option in {lang.Code()} is too long.";
            }
        return null;
    }
}
