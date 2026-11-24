using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mingle.Engine;

namespace Mingle.Client.Services;

/// <summary>
/// A "Fill-in stories" game on one phone: who plays, the theme and length, and how it is played.
/// Blind: the words are collected first and the story is read at the end. Live: read along, and the
/// others shout a word at each gap.
/// </summary>
public sealed class StorySession(HttpClient http)
{
    public ImmutableArray<string> Names { get; private set; } = [];
    public string? Theme { get; private set; }
    public string Topic { get; private set; } = "random";
    public bool Long { get; private set; }
    public bool Blind { get; private set; } = true;
    public bool Ready => !Names.IsEmpty;

    /// <param name="theme">The players' own words, if any.</param>
    /// <param name="topic">The theme chip; the server picks a fresh premise from its group every story.</param>
    public void Start(IEnumerable<string> names, string? theme, string topic, bool longStory, bool blind)
    {
        Names = [.. names];
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
        };
        using var response = await http.PostAsync("api/stories", new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"));
        if (!response.IsSuccessStatusCode) return (null, false);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        try
        {
            return (Story.Parse(root.GetProperty("title").GetString()!, root.GetProperty("text").GetString()!, Names),
                    root.GetProperty("fromLibrary").GetBoolean());
        }
        catch (FormatException)
        {
            return (null, false);
        }
    }
}
