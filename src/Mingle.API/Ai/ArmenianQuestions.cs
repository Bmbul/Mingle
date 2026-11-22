using System.Text.RegularExpressions;

namespace Mingle.API.Ai;

/// <summary>
/// Armenian marks a question with «՞» on the stressed vowel of the question word (Ի՞նչ, Ո՞ր, Ինչո՞ւ).
/// Models sometimes leave it out, so a question without any «՞» gets it on its first question word.
/// </summary>
public static partial class ArmenianQuestions
{
    // Question word without the mark → with it, lowercase.
    private static readonly Dictionary<string, string> Marked = new()
    {
        ["ինչ"] = "ի՞նչ", ["ինչի"] = "ինչի՞", ["ինչով"] = "ինչո՞վ", ["ինչու"] = "ինչո՞ւ",
        ["ինչպես"] = "ինչպե՞ս", ["ինչպիսի"] = "ինչպիսի՞", ["ինչքան"] = "ինչքա՞ն",
        ["ով"] = "ո՞վ", ["ում"] = "ո՞ւմ", ["ումից"] = "ումի՞ց", ["ումով"] = "ումո՞վ",
        ["որ"] = "ո՞ր", ["որն"] = "ո՞րն", ["որը"] = "ո՞րը", ["որի"] = "որի՞", ["որով"] = "որո՞վ",
        ["որտեղ"] = "որտե՞ղ", ["որտեղից"] = "որտեղի՞ց", ["որքան"] = "որքա՞ն",
        ["ուր"] = "ո՞ւր", ["երբ"] = "ե՞րբ", ["քանի"] = "քանի՞",
    };

    [GeneratedRegex(@"\p{L}+")]
    private static partial Regex Word();

    /// <summary>Adds the missing «՞» and replaces a Latin "?" at the end with «։». Text that has «՞» is left as it is.</summary>
    public static string Fix(string text)
    {
        var result = text.TrimEnd();
        if (result.EndsWith('?')) result = result[..^1].TrimEnd() + "։";
        if (result.Contains('՞')) return result;

        foreach (Match word in Word().Matches(result))
        {
            var lower = word.Value.ToLowerInvariant();
            if (!Marked.TryGetValue(lower, out var marked)) continue;
            // Keep a capital first letter (Ինչ → Ի՞նչ).
            if (char.IsUpper(word.Value[0])) marked = char.ToUpperInvariant(marked[0]) + marked[1..];
            return result[..word.Index] + marked + result[(word.Index + word.Length)..];
        }
        return result;
    }
}
