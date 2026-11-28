using System.Globalization;
using System.Text;
using Mingle.DAL.Seeding;
using Mingle.Engine;

namespace Mingle.DAL.Content;

/// <summary>
/// The PostgreSQL statements a <see cref="ContentMigration"/> runs. Upserts by stable id, so running them on a
/// database that already has the rows updates them instead of failing. Rows made by players or the AI are never touched.
/// </summary>
public static class ContentSql
{
    public static IEnumerable<string> Apply(ContentChange change)
    {
        if (!change.Topics.IsEmpty) yield return Topics(change.Topics);
        if (!change.Stories.IsEmpty) yield return Stories(change.Stories);

        var cards = change.Decks.SelectMany(d => d.Cards).DistinctBy(c => c.Id).ToList();
        if (cards.Count > 0)
        {
            yield return Cards(cards);
            foreach (var sql in Options(cards)) yield return sql;
            foreach (var sql in CardTopics(cards)) yield return sql;
        }
        if (!change.Decks.IsEmpty)
        {
            yield return Decks(change.Decks);
            foreach (var sql in DeckCards(change.Decks)) yield return sql;
        }

        foreach (var sql in Remove(change.RemovedDecks, change.RemovedCards, change.RemovedStories, [])) yield return sql;
    }

    /// <summary>Deletes what <see cref="Apply"/> added (texts it changed are not put back).</summary>
    public static IEnumerable<string> Undo(ContentChange change) =>
        Remove([.. change.Decks.Select(d => d.Id)], [.. change.Decks.SelectMany(d => d.Cards).Select(c => c.Id)],
            [.. change.Stories.Select(s => s.Key)], [.. change.Topics.Select(t => t.Code)]);

    private static IEnumerable<string> Remove(
        IReadOnlyCollection<string> decks, IReadOnlyCollection<string> cards, IReadOnlyCollection<string> stories, IReadOnlyCollection<string> topics)
    {
        if (decks.Count > 0)
        {
            var ids = IdList("deck", decks);
            yield return $"DELETE FROM deck_cards WHERE deck_id IN ({ids});";
            yield return $"DELETE FROM decks WHERE id IN ({ids}) AND edit_key_hash IS NULL;";
        }
        if (cards.Count > 0)
        {
            var ids = IdList("card", cards);
            yield return $"DELETE FROM deck_cards d USING decks k WHERE d.deck_id = k.id AND k.edit_key_hash IS NULL AND d.card_id IN ({ids});";
            // A player's deck may still hold the card; then it stays.
            yield return $"DELETE FROM cards c WHERE c.id IN ({ids}) AND NOT EXISTS (SELECT 1 FROM deck_cards d WHERE d.card_id = c.id);";
        }
        if (stories.Count > 0)
            yield return $"DELETE FROM stories WHERE id IN ({IdList("story", stories)});";
        if (topics.Count > 0)
            yield return $"DELETE FROM topics t WHERE t.id IN ({IdList("topic", topics)}) AND NOT EXISTS (SELECT 1 FROM card_topics c WHERE c.topic_id = t.id);";
    }

    private static string Topics(IEnumerable<TopicRow> topics) =>
        Insert("topics", "id, code, name_hy, name_ru, name_en, pickable, about, audience, created_at",
            topics.Select((t, i) => $"{Id("topic", t.Code)}, {S(t.Code)}, {S(t.Hy)}, {S(t.Ru)}, {S(t.En)}, {(t.Pickable ? "TRUE" : "FALSE")}, {S(t.About)}, {S(t.Audience.ToString())}, {Now(i)}"),
            "code = EXCLUDED.code, name_hy = EXCLUDED.name_hy, name_ru = EXCLUDED.name_ru, name_en = EXCLUDED.name_en, pickable = EXCLUDED.pickable, about = EXCLUDED.about, audience = EXCLUDED.audience");

    private static string Stories(IEnumerable<StoryRow> stories) =>
        Insert("stories", "id, lang, theme, title, text, gaps, source, created_at",
            stories.Select((s, i) => $"{Q(s.Id)}, {S(s.Lang.ToString())}, {S(s.Theme.ToString())}, {S(s.Title)}, {S(s.Text)}, {s.Gaps}, 'Curated', {Now(i)}"),
            "lang = EXCLUDED.lang, theme = EXCLUDED.theme, title = EXCLUDED.title, text = EXCLUDED.text, gaps = EXCLUDED.gaps, source = EXCLUDED.source");

    private static string Cards(IEnumerable<Engine.Card> cards) =>
        Insert("cards", "id, kind_id, depth, text_hy, text_ru, text_en, source, created_at",
            cards.Select((c, i) => $"{Id("card", c.Id)}, {Entities.CardKind.Ids[c.KindId]}, {c.Depth}, {S(Text(c, Lang.Hy))}, {S(Text(c, Lang.Ru))}, {S(Text(c, Lang.En))}, 'Curated', {Now(i)}"),
            "kind_id = EXCLUDED.kind_id, depth = EXCLUDED.depth, text_hy = EXCLUDED.text_hy, text_ru = EXCLUDED.text_ru, text_en = EXCLUDED.text_en, source = EXCLUDED.source");

    // Options are matched by position, so their ids (and anything that refers to them) are kept.
    private static IEnumerable<string> Options(IReadOnlyList<Engine.Card> cards)
    {
        var rows = new List<string>();
        foreach (var card in cards)
        {
            var count = card.Texts.Values.Max(t => t.Options.Length);
            for (var position = 0; position < count; position++)
                rows.Add($"{Id("option", $"{card.Id}/{position}")}, {Id("card", card.Id)}, {position}, {S(Option(card, Lang.Hy, position))}, {S(Option(card, Lang.Ru, position))}, {S(Option(card, Lang.En, position))}");
        }
        yield return "DELETE FROM card_options o USING (VALUES "
            + string.Join(", ", cards.Select(c => $"({Id("card", c.Id)}, {c.Texts.Values.Max(t => t.Options.Length)})"))
            + ") AS k(card_id, count) WHERE o.card_id = k.card_id AND o.position >= k.count;";
        if (rows.Count > 0)
            yield return Insert("card_options", "id, card_id, position, text_hy, text_ru, text_en", rows,
                "position = EXCLUDED.position, text_hy = EXCLUDED.text_hy, text_ru = EXCLUDED.text_ru, text_en = EXCLUDED.text_en");
    }

    private static IEnumerable<string> CardTopics(IReadOnlyList<Engine.Card> cards)
    {
        yield return $"DELETE FROM card_topics WHERE card_id IN ({IdList("card", cards.Select(c => c.Id))});";
        var rows = cards.SelectMany(c => c.Topics.Distinct().Select(t => $"{Id("card", c.Id)}, {Id("topic", t)}")).ToList();
        if (rows.Count > 0) yield return Insert("card_topics", "card_id, topic_id", rows, null);
    }

    private static string Decks(IEnumerable<Engine.Deck> decks) =>
        Insert("decks", "id, name_hy, name_ru, name_en, created_at",
            decks.Select((d, i) => $"{Id("deck", d.Id)}, {S(d.Names.GetValueOrDefault(Lang.Hy))}, {S(d.Names.GetValueOrDefault(Lang.Ru))}, {S(d.Names.GetValueOrDefault(Lang.En))}, {Now(i)}"),
            "name_hy = EXCLUDED.name_hy, name_ru = EXCLUDED.name_ru, name_en = EXCLUDED.name_en");

    // A deck lists all its cards: links to cards no longer listed go, the others get their new positions.
    private static IEnumerable<string> DeckCards(IReadOnlyList<Engine.Deck> decks)
    {
        yield return $"DELETE FROM deck_cards WHERE deck_id IN ({IdList("deck", decks.Select(d => d.Id))});";
        var rows = decks.SelectMany(d => d.Cards.Select((c, position) => $"{Id("deck", d.Id)}, {Id("card", c.Id)}, {position}")).ToList();
        if (rows.Count > 0) yield return Insert("deck_cards", "deck_id, card_id, position", rows, null);
    }

    private static string Insert(string table, string columns, IEnumerable<string> rows, string? update)
    {
        var sql = new StringBuilder($"INSERT INTO {table} ({columns}) VALUES\n");
        sql.AppendJoin(",\n", rows.Select(r => $"({r})"));
        var key = table switch { "card_topics" => "card_id, topic_id", "deck_cards" => "deck_id, card_id", _ => "id" };
        sql.Append(update is null ? $"\nON CONFLICT ({key}) DO NOTHING;" : $"\nON CONFLICT ({key}) DO UPDATE SET {update};");
        return sql.ToString();
    }

    private static string? Text(Engine.Card card, Lang lang) => card.Texts.TryGetValue(lang, out var t) ? t.Text : null;

    private static string? Option(Engine.Card card, Lang lang, int position) =>
        card.Texts.TryGetValue(lang, out var t) && position < t.Options.Length ? t.Options[position] : null;

    // New rows keep the order they are listed in (starter decks are shown oldest first).
    private static string Now(int index) => $"now() + interval '{index.ToString(CultureInfo.InvariantCulture)} milliseconds'";

    private static string Id(string kind, string key) => Q(StableId.For(kind, key));

    private static string IdList(string kind, IEnumerable<string> keys) => string.Join(", ", keys.Select(k => Id(kind, k)));

    private static string Q(Guid id) => $"'{id}'::uuid";

    private static string S(string? value) => value is null ? "NULL" : $"'{value.Replace("'", "''")}'";
}
