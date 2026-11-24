using System.Collections.Immutable;
using System.Text;

namespace Mingle.Engine;

/// <summary>
/// A piece of a story: plain text, or a gap with a hint such as "a food". <paramref name="Word"/> is which of the
/// players' words goes there; a gap can come back later in the story with the same word (<paramref name="Repeat"/>).
/// </summary>
public sealed record StoryPart(string? Text, string? Hint, int? Word = null, bool Repeat = false)
{
    public bool IsGap => Hint is not null;
}

/// <summary>
/// A short funny story with gaps, for the "Fill-in stories" game. Written as a template:
/// <c>{food}</c> is a gap whose hint is "food"; <c>{animal#1}</c> is a gap whose word comes back later wherever
/// <c>{#1}</c> appears; <c>{@}</c> is a player's name (the players take turns, in order). Anything else is text.
/// </summary>
public sealed record Story(string Title, ImmutableArray<StoryPart> Parts)
{
    public const int MaxHintLength = 40;

    /// <summary>The gaps the players fill, each once (a returning word is not asked for again).</summary>
    public ImmutableArray<StoryPart> Gaps => [.. Parts.Where(p => p.IsGap && !p.Repeat)];

    /// <summary>Reads a template; throws <see cref="FormatException"/> when it is malformed.</summary>
    public static Story Parse(string title, string template, IReadOnlyList<string> playerNames)
    {
        if (string.IsNullOrWhiteSpace(template)) throw new FormatException("The story is empty.");
        var parts = ImmutableArray.CreateBuilder<StoryPart>();
        var text = new StringBuilder();
        var nameIndex = 0;
        var words = 0;
        var marked = new Dictionary<string, (int Word, string Hint)>(StringComparer.Ordinal);

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
            if (text.Length > 0) { parts.Add(new StoryPart(text.ToString(), null)); text.Clear(); }

            // {#1}: the word given for {hint#1} comes back here.
            if (inside.StartsWith('#'))
            {
                if (!marked.TryGetValue(inside[1..].Trim(), out var earlier)) throw new FormatException($"'{{{inside}}}' comes before its gap.");
                parts.Add(new StoryPart(null, earlier.Hint, earlier.Word, Repeat: true));
                continue;
            }

            // {hint#1}: a gap whose word comes back later.
            var mark = inside.IndexOf('#');
            var hint = (mark < 0 ? inside : inside[..mark]).Trim();
            if (hint.Length == 0 || hint.Length > MaxHintLength) throw new FormatException("A gap needs a short hint.");
            if (mark >= 0)
            {
                var key = inside[(mark + 1)..].Trim();
                if (key.Length == 0 || !marked.TryAdd(key, (words, hint))) throw new FormatException($"Gap mark '#{key}' is empty or used twice.");
            }
            parts.Add(new StoryPart(null, hint, words++));
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
        System.Text.RegularExpressions.Regex.Matches(template, @"\{(?!\s*[@#])[^{}]+\}").Count;

    /// <summary>How many times gap words appear in the story text, returning words included.</summary>
    public int WordSlots => Parts.Count(p => p.IsGap);

    /// <summary>The story with each gap replaced by the word given for it, in order; missing words keep their hint.</summary>
    public string Fill(IReadOnlyList<string> words)
    {
        var text = new StringBuilder();
        foreach (var part in Parts)
            text.Append(part.IsGap ? WordFor(part, words) : part.Text);
        return text.ToString();
    }

    /// <summary>The word for a gap (the same word everywhere it returns), or its hint in brackets while unknown.</summary>
    public static string WordFor(StoryPart part, IReadOnlyList<string> words) =>
        part.Word is { } i && i < words.Count && !string.IsNullOrWhiteSpace(words[i]) ? words[i].Trim() : $"[{part.Hint}]";
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
