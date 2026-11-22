using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mingle.DAL.Entities;
using Mingle.Engine;
using GameCard = Mingle.Engine.Card;
using GameDeck = Mingle.Engine.Deck;

namespace Mingle.DAL.Seeding;

/// <summary>
/// Imports the starter content: <c>Seed/topics.json</c> and <c>Seed/Decks/*.json</c>. Safe to run on
/// every start: seeded rows have stable ids, so existing ones are updated (texts, options, topics,
/// deck contents) and only new ones are added. Rows made by players or the AI are never touched.
/// </summary>
public sealed class DeckSeeder(MingleDbContext db, ILogger<DeckSeeder> log)
{
    public static string DefaultFolder => Path.Combine(AppContext.BaseDirectory, "Seed");

    public async Task SeedAsync(string? seedFolder = null, CancellationToken ct = default)
    {
        var folder = seedFolder ?? DefaultFolder;

        var topics = await SeedTopicsAsync(TopicSeed.Parse(await File.ReadAllTextAsync(Path.Combine(folder, "topics.json"), ct)), ct);

        var decks = new List<GameDeck>();
        foreach (var file in Directory.GetFiles(Path.Combine(folder, "Decks"), "*.json").Order())
            decks.Add(DeckJson.Parse(await File.ReadAllTextAsync(file, ct)));

        var cards = await SeedCardsAsync(decks.SelectMany(d => d.Cards).DistinctBy(c => c.Id).ToList(), topics, ct);
        foreach (var deck in decks) await SeedDeckAsync(deck, cards, ct);

        await db.SaveChangesAsync(ct);
        log.LogInformation("Seed content is up to date: {Topics} topics, {Cards} cards, {Decks} decks.", topics.Count, cards.Count, decks.Count);
    }

    private async Task<Dictionary<string, Topic>> SeedTopicsAsync(IReadOnlyList<TopicSeed> seeds, CancellationToken ct)
    {
        var codes = seeds.Select(s => s.Code).ToList();
        var existing = await db.Topics.Where(t => codes.Contains(t.Code)).ToDictionaryAsync(t => t.Code, ct);

        foreach (var seed in seeds)
        {
            if (!existing.TryGetValue(seed.Code, out var topic))
            {
                topic = new Topic { Id = StableId.For("topic", seed.Code), Code = seed.Code };
                db.Topics.Add(topic);
                existing[seed.Code] = topic;
            }
            topic.Name = LocalizedText.From(seed.Name);
            topic.Pickable = seed.Pickable;
            topic.Audience = seed.Audience;
        }
        return existing;
    }

    private async Task<Dictionary<string, Entities.Card>> SeedCardsAsync(
        IReadOnlyList<GameCard> seeds, IReadOnlyDictionary<string, Topic> topics, CancellationToken ct)
    {
        var ids = seeds.Select(s => StableId.For("card", s.Id)).ToList();
        var existing = await db.Cards
            .Include(c => c.Options)
            .Include(c => c.Topics)
            .Where(c => ids.Contains(c.Id))
            .AsSplitQuery()
            .ToDictionaryAsync(c => c.Id, ct);

        var bySeedKey = new Dictionary<string, Entities.Card>();
        foreach (var seed in seeds)
        {
            var id = StableId.For("card", seed.Id);
            if (!existing.TryGetValue(id, out var card))
            {
                card = new Entities.Card { Id = id };
                db.Cards.Add(card);
            }

            card.KindId = Entities.CardKind.Ids[seed.KindId];
            card.Depth = seed.Depth;
            card.Source = CardSource.Curated;
            card.Text = LocalizedText.From(seed.Texts.ToDictionary(t => t.Key, t => t.Value.Text));
            SyncOptions(seed, card);
            SyncTopics(seed, card, topics);
            bySeedKey[seed.Id] = card;
        }
        return bySeedKey;
    }

    // Options are matched by position, so existing option ids (and any statistics on them) are kept.
    private void SyncOptions(GameCard seed, Entities.Card card)
    {
        var count = seed.Texts.Values.Max(t => t.Options.Length);
        for (var position = 0; position < count; position++)
        {
            var option = card.Options.FirstOrDefault(o => o.Position == position);
            if (option is null)
            {
                option = new CardOption { Id = StableId.For("option", $"{seed.Id}/{position}"), CardId = card.Id, Position = position };
                card.Options.Add(option);
            }
            var text = new LocalizedText();
            foreach (var (lang, cardText) in seed.Texts)
                if (position < cardText.Options.Length) text.Set(lang, cardText.Options[position]);
            option.Text = text;
        }

        foreach (var extra in card.Options.Where(o => o.Position >= count).ToList())
        {
            card.Options.Remove(extra);
            db.CardOptions.Remove(extra);
        }
    }

    private void SyncTopics(GameCard seed, Entities.Card card, IReadOnlyDictionary<string, Topic> topics)
    {
        var wanted = seed.Topics
            .Select(code => topics.TryGetValue(code, out var topic)
                ? topic.Id
                : throw new InvalidDataException($"Card '{seed.Id}' uses topic '{code}', which is not in topics.json."))
            .ToHashSet();

        foreach (var link in card.Topics.Where(l => !wanted.Contains(l.TopicId)).ToList())
        {
            card.Topics.Remove(link);
            db.CardTopics.Remove(link);
        }
        foreach (var topicId in wanted.Where(id => card.Topics.All(l => l.TopicId != id)))
            card.Topics.Add(new CardTopic { CardId = card.Id, TopicId = topicId });
    }

    private async Task SeedDeckAsync(GameDeck seed, IReadOnlyDictionary<string, Entities.Card> cards, CancellationToken ct)
    {
        var id = StableId.For("deck", seed.Id);
        var deck = await db.Decks.Include(d => d.Cards).FirstOrDefaultAsync(d => d.Id == id, ct);
        if (deck is null)
        {
            deck = new Entities.Deck { Id = id };
            db.Decks.Add(deck);
        }
        deck.Name = LocalizedText.From(seed.Names);

        var wanted = seed.Cards.Select((c, position) => (CardId: cards[c.Id].Id, Position: position)).ToList();
        foreach (var link in deck.Cards.Where(l => wanted.All(w => w.CardId != l.CardId)).ToList())
        {
            deck.Cards.Remove(link);
            db.DeckCards.Remove(link);
        }
        foreach (var (cardId, position) in wanted)
        {
            var link = deck.Cards.FirstOrDefault(l => l.CardId == cardId);
            if (link is null) deck.Cards.Add(new DeckCard { DeckId = deck.Id, CardId = cardId, Position = position });
            else link.Position = position;
        }
    }
}
