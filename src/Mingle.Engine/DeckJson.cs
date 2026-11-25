using System.Collections.Immutable;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Mingle.Engine;

/// <summary>
/// Reads a deck file:
/// <code>
/// { "id": "...", "name": { "en": "...", "hy": "...", "ru": "..." },
///   "cards": [ { "id": "...", "kind": "wyr", "depth": 1, "topics": ["travel"],
///                "text": { "en": "...", ... }, "options": { "en": ["A", "B"], ... } } ] }
/// </code>
/// Every problem is reported with the card id, so content mistakes are easy to find.
/// </summary>
public static class DeckJson
{
    public static Deck Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return ReadDeck(doc.RootElement);
    }

    /// <summary>Reads a JSON array of decks, as returned by the server's <c>/api/decks</c>.</summary>
    public static ImmutableArray<Deck> ParseMany(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) throw new FormatException("Expected a JSON array of decks.");
        return [.. doc.RootElement.EnumerateArray().Select(ReadDeck)];
    }

    /// <summary>Writes decks in the same format <see cref="ParseMany"/> reads.</summary>
    public static string WriteMany(IEnumerable<Deck> decks)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            w.WriteStartArray();
            foreach (var deck in decks) WriteDeck(w, deck);
            w.WriteEndArray();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>Writes one deck in the same format <see cref="Parse"/> reads.</summary>
    public static string Write(Deck deck)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
            WriteDeck(w, deck);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteDeck(Utf8JsonWriter w, Deck deck)
    {
        w.WriteStartObject();
        w.WriteString("id", deck.Id);
        WriteStrings(w, "name", deck.Names);
        w.WriteStartArray("cards");
        foreach (var card in deck.Cards)
        {
            w.WriteStartObject();
            w.WriteString("id", card.Id);
            w.WriteString("kind", card.KindId);
            w.WriteNumber("depth", card.Depth);
            w.WriteStartArray("topics");
            foreach (var topic in card.Topics) w.WriteStringValue(topic);
            w.WriteEndArray();
            WriteStrings(w, "text", card.Texts.ToImmutableDictionary(t => t.Key, t => t.Value.Text));
            if (card.Texts.Values.Any(t => !t.Options.IsEmpty))
            {
                w.WriteStartObject("options");
                foreach (var (lang, text) in card.Texts.OrderBy(t => t.Key))
                {
                    w.WriteStartArray(lang.Code());
                    foreach (var option in text.Options) w.WriteStringValue(option);
                    w.WriteEndArray();
                }
                w.WriteEndObject();
            }
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteEndObject();
    }

    private static void WriteStrings(Utf8JsonWriter w, string name, IReadOnlyDictionary<Lang, string> values)
    {
        w.WriteStartObject(name);
        foreach (var (lang, value) in values.OrderBy(v => v.Key)) w.WriteString(lang.Code(), value);
        w.WriteEndObject();
    }

    private static Deck ReadDeck(JsonElement root)
    {
        var deckId = RequiredString(root, "id", "deck");
        var names = ReadStrings(root.GetProperty("name"), $"deck '{deckId}' name");

        var cards = ImmutableArray.CreateBuilder<Card>();
        var seen = new HashSet<string>();
        foreach (var element in root.GetProperty("cards").EnumerateArray())
        {
            var card = ReadCard(element);
            if (!seen.Add(card.Id)) throw new FormatException($"Card id '{card.Id}' appears twice in deck '{deckId}'.");
            cards.Add(card);
        }

        return new Deck(deckId, names, cards.ToImmutable());
    }

    private static Card ReadCard(JsonElement element)
    {
        var id = RequiredString(element, "id", "card");
        var where = $"card '{id}'";

        var kindId = RequiredString(element, "kind", where);
        if (!CardKinds.TryGet(kindId, out var kind)) throw new FormatException($"{where}: unknown kind '{kindId}'.");

        var depth = element.TryGetProperty("depth", out var d) && d.TryGetInt32(out var depthValue)
            ? depthValue
            : throw new FormatException($"{where}: missing depth.");
        if (depth is < 1 or > 3) throw new FormatException($"{where}: depth must be 1, 2 or 3.");

        var topics = element.TryGetProperty("topics", out var t)
            ? t.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToImmutableArray()
            : [];

        var texts = ReadStrings(element.GetProperty("text"), where);
        var options = element.TryGetProperty("options", out var o)
            ? o.EnumerateObject().ToImmutableDictionary(
                p => ParseLang(p.Name, where),
                p => p.Value.EnumerateArray().Select(x => x.GetString() ?? "").ToImmutableArray())
            : ImmutableDictionary<Lang, ImmutableArray<string>>.Empty;

        var needsOptions = kind.CardsHaveOptions;
        var result = ImmutableDictionary.CreateBuilder<Lang, CardText>();
        foreach (var (lang, text) in texts)
        {
            var opts = options.GetValueOrDefault(lang, []);
            if (needsOptions && (opts.Length != 2 || opts.Any(string.IsNullOrWhiteSpace)))
                throw new FormatException($"{where}: '{lang.Code()}' needs exactly 2 options.");
            if (!needsOptions && opts.Length > 0)
                throw new FormatException($"{where}: only 'wyr' cards have options.");
            result[lang] = new CardText(text, opts);
        }

        foreach (var lang in options.Keys.Where(l => !texts.ContainsKey(l)))
            throw new FormatException($"{where}: options for '{lang.Code()}' but no text.");

        return new Card(id, kindId, depth, topics, result.ToImmutable());
    }

    private static ImmutableDictionary<Lang, string> ReadStrings(JsonElement element, string where)
    {
        var result = ImmutableDictionary.CreateBuilder<Lang, string>();
        foreach (var property in element.EnumerateObject())
        {
            var value = property.Value.GetString();
            if (string.IsNullOrWhiteSpace(value)) throw new FormatException($"{where}: empty '{property.Name}' text.");
            result[ParseLang(property.Name, where)] = value.Trim();
        }
        if (result.Count == 0) throw new FormatException($"{where}: no text in any language.");
        return result.ToImmutable();
    }

    private static Lang ParseLang(string code, string where) =>
        Langs.TryParse(code, out var lang) ? lang : throw new FormatException($"{where}: unknown language '{code}'.");

    private static string RequiredString(JsonElement element, string name, string where) =>
        element.TryGetProperty(name, out var value) && value.GetString() is { Length: > 0 } s
            ? s
            : throw new FormatException($"{where}: missing '{name}'.");
}
