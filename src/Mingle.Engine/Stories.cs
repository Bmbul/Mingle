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

            if (inside.StartsWith('@'))
            {
                // A player's name, taking turns, with an Armenian ending if the slot asks for one ({@ը}, {@ին}…);
                // with no players it stays a gap the group fills.
                if (playerNames.Count > 0)
                {
                    var next = template[(end + 1)..].TrimStart();
                    text.Append(ArmenianNames.WithEnding(playerNames[nameIndex++ % playerNames.Count], inside[1..].Trim(), next));
                    continue;
                }
                inside = "name";
            }
            if (inside.Length == 0 || inside.Length > MaxHintLength) throw new FormatException("A gap needs a short hint.");

            if (text.Length > 0) { parts.Add(new StoryPart(text.ToString(), null)); text.Clear(); }
            parts.Add(new StoryPart(null, inside));
        }
        if (text.Length > 0) parts.Add(new StoryPart(text.ToString(), null));

        // A name slot in the title gets the first player's name (with its ending); any other braces there are dropped.
        var cleanTitle = global::System.Text.RegularExpressions.Regex.Replace(title, @"\{@([^{}]*)\}",
            m => playerNames.Count > 0 ? ArmenianNames.WithEnding(playerNames[0], m.Groups[1].Value.Trim(), "") : "");
        cleanTitle = cleanTitle.Replace("{", "").Replace("}", "").Trim();
        var story = new Story(cleanTitle, parts.ToImmutable());
        if (story.Gaps.IsEmpty) throw new FormatException("The story has no gaps.");
        return story;
    }

    /// <summary>Number of gaps in a template, without reading it fully (for checks before parsing).</summary>
    public static int CountGaps(string template) =>
        System.Text.RegularExpressions.Regex.Matches(template, @"\{(?!\s*@)[^{}]+\}").Count;

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

/// <summary>
/// Armenian endings on a player's name, so stories read «Սմբատը և Անին», not «Սմբատ և Անի». The writer marks the
/// role ({@ը} subject, {@ի} "of", {@ին} "to", {@ից} "from", {@ով} "with") and the app adds the right ending for
/// the actual name. An empty ending leaves the name as it is.
/// </summary>
public static class ArmenianNames
{
    private const string Vowels = "աեէըիոօAEIOUaeiouyY";

    public static string WithEnding(string name, string ending, string followingText)
    {
        if (ending.Length == 0 || name.Length == 0) return name;
        var last = name[^1];
        var endsInVowel = Vowels.Contains(last);
        // "ու" ends in a vowel sound although its last letter is "ւ".
        if (name.EndsWith("ու", StringComparison.Ordinal)) endsInVowel = true;
        var glide = last is 'ա' or 'ո' or 'օ' or 'a' or 'o' || (endsInVowel && last is not ('ի' or 'i' or 'ւ' or 'ե' or 'e' or 'է'));

        // A name in Latin letters takes its Armenian ending after a hyphen: «Ani-ն».
        var join = name.Any(ch => ch is >= 'Ա' and <= 'ֆ') ? "" : "-";
        return name + join + Ending(ending, endsInVowel, glide, followingText);
    }

    private static string Ending(string ending, bool endsInVowel, bool glide, string followingText) =>
        ending switch
        {
            // The definite article: -ն after a vowel or before a word starting with a vowel, otherwise -ը.
            "ը" or "ն" => endsInVowel || StartsWithVowel(followingText) ? "ն" : "ը",
            "ի" => glide ? "յի" : "ի",
            "ին" => glide ? "յին" : "ին",
            "ից" => glide ? "յից" : "ից",
            "ով" => glide ? "յով" : "ով",
            _ => ending,
        };

    /// <summary>A vowel sound: «ո» alone is "vo" and «և» is "yev", so only «ու» counts among those.</summary>
    private static bool StartsWithVowel(string text) =>
        text.Length > 0 && ("աեէըիօԱԵԷԸԻՕ".Contains(text[0]) || text.StartsWith("ու") || text.StartsWith("Ու"));
}
