using System.Collections.Immutable;
using System.Net.Http;
using System.Text.Json;
using Microsoft.JSInterop;
using Mingle.Engine;

namespace Mingle.Client.Services;

/// <summary>
/// Loads decks from the server. The last good copy is kept in the browser, so pass-and-play
/// still works offline or when the server is unreachable.
/// </summary>
public sealed class ContentService(HttpClient http, IJSRuntime js)
{
    private ImmutableArray<Deck>? decks;

    public async Task<ImmutableArray<Deck>> GetDecksAsync()
    {
        if (decks is { } cached) return cached;

        try
        {
            var json = await http.GetStringAsync("api/decks");
            var loaded = DeckJson.ParseMany(json);
            await js.InvokeVoidAsync("mingle.saveDecks", json);
            decks = loaded;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or FormatException)
        {
            var saved = await js.InvokeAsync<string?>("mingle.loadDecks");
            if (saved is null) throw new ContentUnavailableException(ex);
            decks = DeckJson.ParseMany(saved);
        }

        return decks.Value;
    }
}

/// <summary>Neither the server nor a saved copy could provide decks.</summary>
public sealed class ContentUnavailableException(Exception inner) : Exception("Decks are unavailable.", inner);
