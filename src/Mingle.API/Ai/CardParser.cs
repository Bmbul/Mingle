using System.Collections.Immutable;
using System.Text.Json;
using Mingle.Engine;

namespace Mingle.API.Ai;

/// <summary>
/// Turns the model's JSON into game cards, keeping only cards that pass every check. AI output is
/// untrusted, so anything malformed is dropped rather than shown to players.
/// </summary>
public static class CardParser
{
    public const int MaxTextLength = 300;
    public const int MaxOptionLength = 120;

    /// <summary>The card text for "would you rather" cards, like the curated decks use.</summary>
    private static readonly ImmutableDictionary<Lang, string> WouldYouRather = new Dictionary<Lang, string>
    {
        [Lang.En] = "Would you rather…",
        [Lang.Hy] = "Ո՞րն ես նախընտրում",
        [Lang.Ru] = "Что ты выберешь?",
    }.ToImmutableDictionary();

    public static ImmutableArray<Card> Parse(string json, CardRequest request)
    {
        JsonDocument document;
        try { document = JsonDocument.Parse(json); }
        catch (JsonException) { return []; }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("cards", out var cards) ||
                cards.ValueKind != JsonValueKind.Array)
                return [];

            var topics = request.Topics.Select(t => t.Code).ToHashSet(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = ImmutableArray.CreateBuilder<Card>();

            foreach (var element in cards.EnumerateArray())
            {
                if (result.Count >= request.Count) break;
                if (TryRead(element, request, topics) is not { } card) continue;
                var key = string.Join("|", card.Texts[request.Languages[0]].Options.Prepend(card.Texts[request.Languages[0]].Text));
                if (seen.Add(key)) result.Add(card);
            }

            return result.ToImmutable();
        }
    }

    private static Card? TryRead(JsonElement element, CardRequest request, HashSet<string> topics)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        if (Str(element, "kind") is not { } kind || !request.AllowedKinds.Contains(kind)) return null;
        if (!element.TryGetProperty("depth", out var d) || !d.TryGetInt32(out var depth) || depth is < 1 or > 3 || !request.Allows(depth)) return null;
        var topic = Str(element, "topic");
        if (topic is null || !topics.Contains(topic)) return null;
        if (!element.TryGetProperty("text", out var text) || text.ValueKind != JsonValueKind.Object) return null;
        element.TryGetProperty("options", out var options);

        var languages = request.Languages;
        var isChoice = kind == "wyr";
        // A "guess" card must be the question itself; "Guess which…" means the model wrote the instruction instead.
        if (kind == "guess" && Str(text, "en") is { } guessText && guessText.TrimStart().StartsWith("Guess", StringComparison.OrdinalIgnoreCase))
            return null;
        var texts = ImmutableDictionary.CreateBuilder<Lang, CardText>();
        foreach (var lang in languages)
        {
            var optionList = ReadOptions(options, lang.Code());
            if (isChoice)
            {
                if (optionList is not { Length: 2 } pair || pair.Any(o => !Clean(o, MaxOptionLength))) return null;
                texts[lang] = new CardText(WouldYouRather[lang], pair);
            }
            else
            {
                if (Str(text, lang.Code()) is not { } value || !Clean(value, MaxTextLength)) return null;
                var tidy = Tidy(value);
                if (kind == CardKinds.Association.Id)
                {
                    // One word, a short phrase, or a starter ending in "…": no question, no full stop, nothing long.
                    tidy = tidy.TrimEnd('.', '։', '!');
                    if (tidy.Contains('?') || tidy.Contains('՞') || tidy.Length > 40 || tidy.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 4) return null;
                }
                // Every kind but "finish the sentence" and associations is a question.
                if (lang == Lang.Hy && kind is not ("finish" or "word")) tidy = ArmenianQuestions.Fix(tidy);
                texts[lang] = new CardText(tidy);
            }
        }

        return new Card($"ai-{Guid.NewGuid():N}", kind, depth, [topic], texts.ToImmutable());
    }

    private static ImmutableArray<string>? ReadOptions(JsonElement options, string lang) =>
        options.ValueKind == JsonValueKind.Object && options.TryGetProperty(lang, out var list) && list.ValueKind == JsonValueKind.Array
            ? [.. list.EnumerateArray().Select(o => o.ValueKind == JsonValueKind.String ? o.GetString()!.Trim() : "")]
            : null;

    private static string? Str(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>Removes quotation marks the model sometimes puts around a card's opening words.</summary>
    private static string Tidy(string value)
    {
        var text = value.Trim();
        foreach (var opening in new[] { "Ո՞վ ավելի շուտ", "Who is most likely", "Кто скорее всего" })
            foreach (var (open, close) in new[] { ("«", "»"), ("\"", "\""), ("“", "”") })
                if (text.StartsWith(open + opening + close, StringComparison.Ordinal))
                    text = opening + text[(open.Length + opening.Length + close.Length)..];
        return text;
    }

    // Non-empty, not too long, and no template placeholders.
    private static bool Clean(string value, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maxLength && !value.Contains('{') && !value.Contains('}');
}
