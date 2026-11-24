using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.JSInterop;
using Mingle.Engine;

namespace Mingle.Client.Services;

/// <summary>
/// A "Fill-in stories" game on one phone: who plays, the theme and length, and how it is played.
/// Blind: the words are collected first and the story is read at the end. Live: read along, and the
/// others shout a word at each gap. The phone remembers which library stories it has played, so they
/// do not come back until all of them have been.
/// </summary>
public sealed class StorySession(HttpClient http, IJSRuntime js)
{
    private const string SeenKey = "mingle.stories.seen";
    private const int MaxSeen = 500;

    public ImmutableArray<string> Names { get; private set; } = [];
    public string? Theme { get; private set; }
    public string Topic { get; private set; } = "random";
    public bool Long { get; private set; }
    public bool Blind { get; private set; } = true;
    public bool Ready => !Names.IsEmpty;
    private int stories;

    /// <param name="theme">The players' own words, if any.</param>
    /// <param name="topic">The theme chip; the server picks a fresh premise from its group every story.</param>
    public void Start(IEnumerable<string> names, string? theme, string topic, bool longStory, bool blind)
    {
        Names = [.. names];
        stories = 0;
        Theme = string.IsNullOrWhiteSpace(theme) ? null : theme.Trim();
        Topic = topic;
        Long = longStory;
        Blind = blind;
    }

    /// <summary>A new story in the reader's language: written by the AI, or a ready-made one.</summary>
    public async Task<(Story? Story, bool FromLibrary)> NextStoryAsync()
    {
        // Built by hand: the trimmed browser app has no reflection-based JSON for anonymous types.
        var body = new JsonObject
        {
            ["lang"] = Text.UiLang.Code(),
            ["theme"] = Theme,
            ["topic"] = Topic,
            ["length"] = Long ? "long" : "short",
            ["names"] = new JsonArray([.. Names.Select(n => (JsonNode?)n)]),
            ["seen"] = new JsonArray([.. (await LoadSeenAsync()).Select(id => (JsonNode?)id)]),
        };
        using var response = await http.PostAsync("api/stories", new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"));
        if (!response.IsSuccessStatusCode) return (null, false);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        try
        {
            // Each story starts with the next player, so everyone gets to be the hero ({@1}).
            var shift = stories++ % Names.Length;
            var story = Story.Parse(root.GetProperty("title").GetString()!, root.GetProperty("text").GetString()!,
                [.. Names.Skip(shift), .. Names.Take(shift)]);
            if (root.TryGetProperty("id", out var id) && id.GetString() is { Length: > 0 } key) await RememberAsync(key);
            return (story, root.GetProperty("fromLibrary").GetBoolean());
        }
        catch (FormatException)
        {
            return (null, false);
        }
    }

    private async Task<List<string>> LoadSeenAsync()
    {
        try
        {
            var saved = await js.InvokeAsync<string?>("mingle.get", SeenKey);
            return string.IsNullOrEmpty(saved) ? [] : [.. saved.Split('|', StringSplitOptions.RemoveEmptyEntries)];
        }
        catch (JSException) { return []; }
    }

    private async Task RememberAsync(string id)
    {
        var seen = await LoadSeenAsync();
        seen.Remove(id);
        seen.Add(id);
        if (seen.Count > MaxSeen) seen.RemoveRange(0, seen.Count - MaxSeen);
        try { await js.InvokeVoidAsync("mingle.set", SeenKey, string.Join('|', seen)); }
        catch (JSException) { }
    }
}
