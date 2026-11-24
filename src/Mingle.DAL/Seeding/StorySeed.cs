using System.Text.Json;
using Mingle.Engine;

namespace Mingle.DAL.Seeding;

/// <summary>
/// One story of <c>Seed/Stories/{lang}.json</c>: <c>{ "lang": "hy", "stories": [ { "id", "theme", "title", "text" } ] }</c>.
/// The seed "id" is only a key for the file; the database id is made from it (<see cref="StableId"/>).
/// </summary>
public sealed record StorySeed(string Key, Lang Lang, StoryTheme Theme, string Title, string Text)
{
    public Guid Id => StableId.For("story", Key);

    /// <summary>How many words the players give, counted with two players.</summary>
    public int Gaps => Story.Parse(Title, Text, ["A", "B"]).Gaps.Length;

    /// <summary>Reads one file; throws <see cref="FormatException"/> on a broken or unknown entry.</summary>
    public static IReadOnlyList<StorySeed> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var langCode = doc.RootElement.GetProperty("lang").GetString();
        if (!Langs.TryParse(langCode, out var lang)) throw new FormatException($"Unknown story language '{langCode}'.");

        var result = new List<StorySeed>();
        foreach (var item in doc.RootElement.GetProperty("stories").EnumerateArray())
        {
            var id = item.GetProperty("id").GetString() ?? "";
            var code = item.GetProperty("theme").GetString();
            if (id.Length == 0) throw new FormatException("A story needs an id.");
            if (StoryShelf.ParseTheme(code) is not { } theme) throw new FormatException($"Story '{id}': unknown theme '{code}'.");
            var seed = new StorySeed(id, lang, theme, item.GetProperty("title").GetString() ?? "", item.GetProperty("text").GetString() ?? "");
            try { _ = seed.Gaps; }
            catch (FormatException ex) { throw new FormatException($"Story '{id}': {ex.Message}", ex); }
            result.Add(seed);
        }

        var duplicate = result.GroupBy(s => s.Key).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null) throw new FormatException($"Story '{duplicate.Key}' appears twice.");
        return result;
    }

    /// <summary>Every story in a folder of story files.</summary>
    public static IReadOnlyList<StorySeed> ReadFolder(string folder) =>
        Directory.Exists(folder)
            ? [.. Directory.GetFiles(folder, "*.json").Order().SelectMany(f => Parse(File.ReadAllText(f)))]
            : [];
}
