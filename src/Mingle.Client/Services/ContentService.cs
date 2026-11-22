using System.Collections.Immutable;
using System.Net.Http;
using Mingle.Engine;

namespace Mingle.Client.Services;

/// <summary>Loads the decks shipped with the app. The service worker caches them, so they work offline.</summary>
public sealed class ContentService(HttpClient http)
{
    private static readonly string[] DeckFiles = ["data/icebreakers.json", "data/couples.json"];
    private ImmutableArray<Deck>? decks;

    public async Task<ImmutableArray<Deck>> GetDecksAsync()
    {
        if (decks is { } cached) return cached;

        var loaded = await Task.WhenAll(DeckFiles.Select(async file => DeckJson.Parse(await http.GetStringAsync(file))));
        decks = [.. loaded];
        return decks.Value;
    }
}
