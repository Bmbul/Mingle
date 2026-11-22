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
public sealed class DecksController(IDeckSource starterDecks, UserDeckStore userDecks) : ControllerBase
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
        foreach (var card in deck.Cards)
            foreach (var (lang, text) in card.Texts)
            {
                if (text.Text.Length > CardParser.MaxTextLength) return $"A card text in {lang.Code()} is too long.";
                if (text.Options.Any(o => o.Length > CardParser.MaxOptionLength)) return $"An option in {lang.Code()} is too long.";
            }
        return null;
    }
}
