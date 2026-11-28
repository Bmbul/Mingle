using System.Collections.Immutable;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.JSInterop;
using Mingle.Engine;

namespace Mingle.Client.Services;

/// <summary>
/// Loads decks from the server: the starter decks plus the decks saved on this device. The last good
/// copy of each is kept in the browser, so pass-and-play still works offline.
/// </summary>
public sealed class ContentService(HttpClient http, IJSRuntime js, DeviceDeckStore deviceDecks)
{
    private ImmutableArray<Deck>? starter;
    private readonly Dictionary<string, Deck> userDecks = [];

    /// <summary>
    /// Raised when a quiet refresh brings newer decks than the saved copy shown so far; pages that list decks
    /// load them again.
    /// </summary>
    public event Action? Updated;

    private Task? refreshing;

    /// <summary>Starter decks first, then this device's decks (newest first).</summary>
    public async Task<ImmutableArray<Deck>> GetDecksAsync()
    {
        var starterDecks = await GetStarterDecksAsync();
        var mine = new List<Deck>();
        foreach (var entry in await deviceDecks.ListAsync())
            if (await GetDeckAsync(entry.Id, savedFirst: true) is { } deck) mine.Add(deck);
        return [.. mine, .. starterDecks];
    }

    /// <summary>
    /// The starter decks. When this device has a saved copy it is returned at once and the server is asked
    /// in the background (a sleeping server can take a while to wake up); only the very first visit waits.
    /// </summary>
    public async Task<ImmutableArray<Deck>> GetStarterDecksAsync()
    {
        if (starter is { } cached) return cached;

        var saved = await js.InvokeAsync<string?>("mingle.loadDecks");
        if (saved is not null && TryParse(saved) is { } fromSaved)
        {
            starter = fromSaved;
            refreshing ??= RefreshStarterAsync(saved);
            return fromSaved;
        }

        try
        {
            starter = await FetchStarterAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or FormatException or TaskCanceledException)
        {
            throw new ContentUnavailableException(ex);
        }
        return starter.Value;
    }

    private async Task<ImmutableArray<Deck>> FetchStarterAsync()
    {
        var json = await http.GetStringAsync("api/decks");
        var loaded = DeckJson.ParseMany(json);
        await js.InvokeVoidAsync("mingle.saveDecks", json);
        return loaded;
    }

    private async Task RefreshStarterAsync(string saved)
    {
        try
        {
            var json = await http.GetStringAsync("api/decks");
            if (json == saved) return;
            starter = DeckJson.ParseMany(json);
            await js.InvokeVoidAsync("mingle.saveDecks", json);
            Updated?.Invoke();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or FormatException or TaskCanceledException)
        {
            // Offline or the server is down: the saved copy stays.
        }
    }

    private static ImmutableArray<Deck>? TryParse(string json)
    {
        try { return DeckJson.ParseMany(json); }
        catch (Exception ex) when (ex is JsonException or FormatException) { return null; }
    }

    /// <summary>A deck by id (from a share link or this device's list); null if it no longer exists.</summary>
    /// <param name="savedFirst">Return this device's saved copy at once and check the server in the background.</param>
    public async Task<Deck?> GetDeckAsync(string id, bool fresh = false, bool savedFirst = false)
    {
        if (!fresh && userDecks.TryGetValue(id, out var cached)) return cached;
        var storageKey = $"mingle.deck.{id}";
        if (savedFirst && !fresh && await js.InvokeAsync<string?>("mingle.get", storageKey) is { } copy)
        {
            try
            {
                var deck = userDecks[id] = DeckJson.Parse(copy);
                _ = RefreshDeckAsync(id, copy);
                return deck;
            }
            catch (Exception ex) when (ex is JsonException or FormatException) { }
        }
        try
        {
            return await FetchDeckAsync(id);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or FormatException or TaskCanceledException)
        {
            var saved = await js.InvokeAsync<string?>("mingle.get", storageKey);
            return saved is null ? null : userDecks[id] = DeckJson.Parse(saved);
        }
    }

    private async Task<Deck?> FetchDeckAsync(string id)
    {
        var storageKey = $"mingle.deck.{id}";
        using var response = await http.GetAsync($"api/decks/{Uri.EscapeDataString(id)}");
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            userDecks.Remove(id);
            await js.InvokeVoidAsync("mingle.remove", storageKey);
            return null;
        }
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var deck = DeckJson.Parse(json);
        await js.InvokeVoidAsync("mingle.set", storageKey, json);
        return userDecks[id] = deck;
    }

    private async Task RefreshDeckAsync(string id, string saved)
    {
        try
        {
            var gone = await FetchDeckAsync(id) is null;
            if (gone || await js.InvokeAsync<string?>("mingle.get", $"mingle.deck.{id}") != saved) Updated?.Invoke();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or FormatException or TaskCanceledException) { }
    }

    /// <summary>Creates a deck on the server and remembers it, with its edit key, on this device.</summary>
    public async Task<DeckSaveResult> CreateAsync(Deck deck)
    {
        using var response = await http.PostAsync("api/decks", Json(deck));
        if (!response.IsSuccessStatusCode) return await Failed(response);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var id = body.RootElement.GetProperty("id").GetString()!;
        await deviceDecks.AddAsync(id, body.RootElement.GetProperty("editKey").GetString());
        await GetDeckAsync(id, fresh: true);
        return new DeckSaveResult(id, null);
    }

    public async Task<DeckSaveResult> SaveAsync(string id, string editKey, Deck deck)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"api/decks/{Uri.EscapeDataString(id)}") { Content = Json(deck) };
        request.Headers.Add("X-Edit-Key", editKey);
        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return await Failed(response);
        await GetDeckAsync(id, fresh: true);
        return new DeckSaveResult(id, null);
    }

    public async Task<bool> DeleteAsync(string id, string editKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/decks/{Uri.EscapeDataString(id)}");
        request.Headers.Add("X-Edit-Key", editKey);
        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound) return false;
        userDecks.Remove(id);
        await js.InvokeVoidAsync("mingle.remove", $"mingle.deck.{id}");
        await deviceDecks.RemoveAsync(id);
        return true;
    }

    private ImmutableArray<TopicInfo>? topics;

    /// <summary>The topics players can pick, with names in each language.</summary>
    public async Task<ImmutableArray<TopicInfo>> GetTopicsAsync()
    {
        if (topics is { } cached) return cached;
        using var doc = JsonDocument.Parse(await http.GetStringAsync("api/topics"));
        topics = [.. doc.RootElement.EnumerateArray().Select(t => new TopicInfo(
            t.GetProperty("code").GetString()!,
            t.GetProperty("name").EnumerateObject()
                .Where(n => Langs.TryParse(n.Name, out _))
                .ToImmutableDictionary(n => { Langs.TryParse(n.Name, out var l); return l; }, n => n.Value.GetString() ?? "")))];
        return topics.Value;
    }

    /// <summary>Asks the AI for new cards; nothing is saved until the deck is.</summary>
    /// <param name="depths">Any of 1–3; all three mixes light to deep.</param>
    /// <param name="languages">The languages to write every card in.</param>
    /// <param name="setting">Who plays: couple, friends, family, party or colleagues.</param>
    public async Task<(ImmutableArray<Card> Cards, string? Error)> WriteCardsAsync(
        IReadOnlyCollection<string> topicCodes, string theme, IReadOnlyCollection<int> depths, string setting,
        IReadOnlyCollection<Lang> languages, int count, DeckStyle style = DeckStyle.Questions, IReadOnlyCollection<string>? moods = null,
        IReadOnlyCollection<string>? kinds = null)
    {
        // Built by hand: the trimmed browser app has no reflection-based JSON for anonymous types.
        var request = new System.Text.Json.Nodes.JsonObject
        {
            ["topics"] = new System.Text.Json.Nodes.JsonArray([.. topicCodes.Select(c => (System.Text.Json.Nodes.JsonNode?)c)]),
            ["theme"] = theme,
            ["depths"] = new System.Text.Json.Nodes.JsonArray([.. depths.Select(d => (System.Text.Json.Nodes.JsonNode?)d)]),
            ["languages"] = new System.Text.Json.Nodes.JsonArray([.. languages.Select(l => (System.Text.Json.Nodes.JsonNode?)l.Code())]),
            ["setting"] = setting,
            ["count"] = count,
            ["style"] = style switch { DeckStyle.Blitz => "associations", DeckStyle.Handoff => "handoff", DeckStyle.Facts => "facts", DeckStyle.Guess => "guess", DeckStyle.Odd => "odd", DeckStyle.Match => "match", DeckStyle.Never => "never", DeckStyle.Mirror => "mirror", DeckStyle.Auction => "auction", _ => "questions" },
            ["moods"] = new System.Text.Json.Nodes.JsonArray([.. (moods ?? []).Select(m => (System.Text.Json.Nodes.JsonNode?)m)]),
            ["kinds"] = new System.Text.Json.Nodes.JsonArray([.. (style is DeckStyle.Questions or DeckStyle.Blitz ? kinds ?? [] : []).Select(k => (System.Text.Json.Nodes.JsonNode?)k)]),
        };
        var body = request.ToJsonString();
        using var response = await http.PostAsync("api/decks/write", new StringContent(body, Encoding.UTF8, new MediaTypeHeaderValue("application/json")));
        if (!response.IsSuccessStatusCode) return ([], (await Failed(response)).Error);
        return (DeckJson.Parse(await response.Content.ReadAsStringAsync()).Cards, null);
    }

    private static StringContent Json(Deck deck) =>
        new(DeckJson.Write(deck), Encoding.UTF8, new MediaTypeHeaderValue("application/json"));

    private static async Task<DeckSaveResult> Failed(HttpResponseMessage response)
    {
        string? message = null;
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.ServiceUnavailable)
        {
            try
            {
                using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                message = body.RootElement.GetProperty("error").GetString();
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException) { }
        }
        return new DeckSaveResult(null, message ?? $"{(int)response.StatusCode}");
    }
}

/// <summary>The saved deck's id, or an error (the server's message, or the HTTP status code).</summary>
public sealed record DeckSaveResult(string? Id, string? Error);

/// <summary>Neither the server nor a saved copy could provide decks.</summary>
public sealed class ContentUnavailableException(Exception inner) : Exception("Decks are unavailable.", inner);
