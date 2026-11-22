using System.Text.Json;
using Mingle.DAL.Entities;
using Mingle.Engine;

namespace Mingle.DAL.Seeding;

/// <summary>One entry of <c>Seed/topics.json</c>.</summary>
public sealed record TopicSeed(string Code, TopicAudience Audience, bool Pickable, IReadOnlyDictionary<Lang, string> Name)
{
    public static IReadOnlyList<TopicSeed> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var result = new List<TopicSeed>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var code = item.GetProperty("code").GetString() ?? throw new FormatException("A topic has no code.");
            var audience = Enum.Parse<TopicAudience>(item.GetProperty("audience").GetString() ?? "everyone", ignoreCase: true);
            var pickable = item.GetProperty("pickable").GetBoolean();
            var name = item.GetProperty("name").EnumerateObject().ToDictionary(
                p => Langs.TryParse(p.Name, out var lang) ? lang : throw new FormatException($"Topic '{code}': unknown language '{p.Name}'."),
                p => p.Value.GetString() ?? "");
            if (Langs.All.Any(l => string.IsNullOrWhiteSpace(name.GetValueOrDefault(l))))
                throw new FormatException($"Topic '{code}' needs a name in every language.");
            result.Add(new TopicSeed(code, audience, pickable, name));
        }

        var duplicate = result.GroupBy(t => t.Code).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null) throw new FormatException($"Topic '{duplicate.Key}' appears twice.");
        return result;
    }
}
