using System.Collections.Immutable;
using System.Text;

namespace Mingle.Engine;

/// <summary>A piece of a story: plain text, or a gap with a hint such as "a food".</summary>
public sealed record StoryPart(string? Text, string? Hint)
{
    public bool IsGap => Hint is not null;
}

/// <summary>
/// A short funny story with gaps, for the "Fill-in stories" game. Written as a template:
/// <c>{food}</c> is a gap whose hint is "food", and <c>{@}</c> is a player's name (the players take turns
/// filling these, in order). Anything else is plain text.
/// </summary>
public sealed record Story(string Title, ImmutableArray<StoryPart> Parts)
{
    public const int MaxHintLength = 40;

    public ImmutableArray<StoryPart> Gaps => [.. Parts.Where(p => p.IsGap)];

    /// <summary>Reads a template; throws <see cref="FormatException"/> when it is malformed.</summary>
    public static Story Parse(string title, string template, IReadOnlyList<string> playerNames)
    {
        if (string.IsNullOrWhiteSpace(template)) throw new FormatException("The story is empty.");
        var parts = ImmutableArray.CreateBuilder<StoryPart>();
        var text = new StringBuilder();
        var nameIndex = 0;

        for (var i = 0; i < template.Length; i++)
        {
            var c = template[i];
            if (c == '}') throw new FormatException("A '}' without a '{'.");
            if (c != '{') { text.Append(c); continue; }

            var end = template.IndexOf('}', i + 1);
            if (end < 0) throw new FormatException("A '{' without a '}'.");
            var inside = template[(i + 1)..end].Trim();
            if (inside.Contains('{')) throw new FormatException("Gaps cannot be nested.");
            i = end;

            if (inside == "@")
            {
                // A player's name, taking turns; with no players it stays a gap the group fills.
                if (playerNames.Count > 0) { text.Append(playerNames[nameIndex++ % playerNames.Count]); continue; }
                inside = "name";
            }
            if (inside.Length == 0 || inside.Length > MaxHintLength) throw new FormatException("A gap needs a short hint.");

            if (text.Length > 0) { parts.Add(new StoryPart(text.ToString(), null)); text.Clear(); }
            parts.Add(new StoryPart(null, inside));
        }
        if (text.Length > 0) parts.Add(new StoryPart(text.ToString(), null));

        // A name slot in the title gets the first player's name; any other braces there are dropped.
        var cleanTitle = title.Replace("{@}", playerNames.Count > 0 ? playerNames[0] : "").Replace("{", "").Replace("}", "").Trim();
        var story = new Story(cleanTitle, parts.ToImmutable());
        if (story.Gaps.IsEmpty) throw new FormatException("The story has no gaps.");
        return story;
    }

    /// <summary>Number of gaps in a template, without reading it fully (for checks before parsing).</summary>
    public static int CountGaps(string template) =>
        System.Text.RegularExpressions.Regex.Matches(template, @"\{(?!\s*@\s*\})[^{}]+\}").Count;

    /// <summary>The story with each gap replaced by the word given for it, in order; missing words keep their hint.</summary>
    public string Fill(IReadOnlyList<string> words)
    {
        var text = new StringBuilder();
        var gap = 0;
        foreach (var part in Parts)
        {
            if (!part.IsGap) { text.Append(part.Text); continue; }
            text.Append(gap < words.Count && !string.IsNullOrWhiteSpace(words[gap]) ? words[gap].Trim() : $"[{part.Hint}]");
            gap++;
        }
        return text.ToString();
    }
}
