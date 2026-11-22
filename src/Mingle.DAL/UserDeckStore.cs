using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Mingle.DAL.Entities;
using GameCard = Mingle.Engine.Card;
using GameDeck = Mingle.Engine.Deck;

namespace Mingle.DAL;

/// <summary>The result of changing a user deck.</summary>
public enum DeckChange { Done, NotFound, WrongKey }

/// <summary>
/// Decks made in the editor. There are no accounts yet, so a deck is owned by whoever holds its
/// edit key: it is returned once at creation, kept on that device, and only its hash is stored.
/// Anyone with a deck's id can play it.
/// </summary>
public sealed class UserDeckStore(MingleDbContext db)
{
    public const int MaxCards = 200;

    /// <summary>Saves a new deck and returns its id and edit key.</summary>
    public async Task<(Guid Id, string EditKey)> CreateAsync(GameDeck content, CancellationToken ct = default)
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var deck = new Deck { EditKeyHash = Hash(key) };
        db.Decks.Add(deck);
        await ApplyAsync(deck, content, ct);
        await db.SaveChangesAsync(ct);
        return (deck.Id, key);
    }

    /// <summary>Any deck by id (user or starter), as the game uses it.</summary>
    public async Task<GameDeck?> GetAsync(Guid id, CancellationToken ct = default) =>
        (await GetManyAsync([id], ct)).FirstOrDefault();

    public async Task<ImmutableArray<GameDeck>> GetManyAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var wanted = ids.Distinct().ToList();
        if (wanted.Count == 0) return [];
        var decks = await LoadDecks().AsNoTracking().Where(d => wanted.Contains(d.Id)).ToListAsync(ct);
        return [.. decks.Select(DeckMapping.ToGame)];
    }

    /// <summary>Replaces a deck's name and cards. The key must match the one given at creation.</summary>
    public async Task<DeckChange> SaveAsync(Guid id, string? editKey, GameDeck content, CancellationToken ct = default)
    {
        var deck = await LoadDecks().FirstOrDefaultAsync(d => d.Id == id, ct);
        var check = Check(deck, editKey);
        if (check != DeckChange.Done) return check;

        deck!.UpdatedAt = DateTimeOffset.UtcNow;
        await ApplyAsync(deck, content, ct);
        await db.SaveChangesAsync(ct);
        return DeckChange.Done;
    }

    public async Task<DeckChange> DeleteAsync(Guid id, string? editKey, CancellationToken ct = default)
    {
        var deck = await LoadDecks().FirstOrDefaultAsync(d => d.Id == id, ct);
        var check = Check(deck, editKey);
        if (check != DeckChange.Done) return check;

        // The deck's own cards go with it; starter cards it used stay. Links go before cards.
        var ownCards = deck!.Cards.Select(l => l.Card).Where(c => c.Source == CardSource.User).ToList();
        db.DeckCards.RemoveRange(deck.Cards);
        db.Decks.Remove(deck);
        db.Cards.RemoveRange(ownCards);
        await db.SaveChangesAsync(ct);
        return DeckChange.Done;
    }

    private IQueryable<Deck> LoadDecks() => db.Decks
        .Include(d => d.Cards).ThenInclude(l => l.Card).ThenInclude(c => c.Options)
        .Include(d => d.Cards).ThenInclude(l => l.Card).ThenInclude(c => c.Topics).ThenInclude(t => t.Topic)
        .AsSplitQuery();

    private static DeckChange Check(Deck? deck, string? editKey)
    {
        if (deck is null) return DeckChange.NotFound;
        if (deck.EditKeyHash is null || string.IsNullOrEmpty(editKey)) return DeckChange.WrongKey;
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(deck.EditKeyHash), Encoding.ASCII.GetBytes(Hash(editKey)))
            ? DeckChange.Done
            : DeckChange.WrongKey;
    }

    private static string Hash(string key) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    /// <summary>
    /// Makes the deck hold exactly <paramref name="content"/>. Cards this deck wrote are updated in
    /// place (so their ids, and later their likes, stay). A starter card is linked as it is; if it
    /// was changed, the change becomes the deck's own copy and the starter card is untouched.
    /// </summary>
    private async Task ApplyAsync(Deck deck, GameDeck content, CancellationToken ct)
    {
        if (content.Cards.Length > MaxCards) throw new FormatException($"A deck can hold up to {MaxCards} cards.");
        deck.Name = LocalizedText.From(content.Names);

        var own = deck.Cards.Select(l => l.Card).Where(c => c.Source == CardSource.User).ToDictionary(c => c.Id);
        var otherIds = content.Cards
            .Select(c => Guid.TryParse(c.Id, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty && !own.ContainsKey(g))
            .ToList();
        var others = await db.Cards
            .Include(c => c.Options)
            .Include(c => c.Topics).ThenInclude(t => t.Topic)
            .Where(c => otherIds.Contains(c.Id) && c.Source != CardSource.User)
            .AsSplitQuery()
            .ToDictionaryAsync(c => c.Id, ct);

        var links = new List<Card>();
        foreach (var wanted in content.Cards)
        {
            Guid.TryParse(wanted.Id, out var id);
            if (own.TryGetValue(id, out var mine))
            {
                Write(mine, wanted);
                links.Add(mine);
            }
            else if (others.TryGetValue(id, out var shared) && Same(DeckMapping.ToGame(shared), wanted))
            {
                links.Add(shared);
            }
            else
            {
                var copy = new Card { Source = CardSource.User, ForkedFromId = others.ContainsKey(id) ? id : null };
                if (others.TryGetValue(id, out var original))
                    copy.Topics.AddRange(original.Topics.Select(t => new CardTopic { CardId = copy.Id, TopicId = t.TopicId }));
                Write(copy, wanted);
                db.Cards.Add(copy);
                links.Add(copy);
            }
        }

        // Links first (a card cannot be deleted while a deck still holds it), then the deck's dropped cards.
        var wantedLinks = links.DistinctBy(c => c.Id).Select((c, position) => (Card: c, Position: position)).ToList();
        foreach (var link in deck.Cards.Where(l => wantedLinks.All(w => w.Card.Id != l.CardId)).ToList())
        {
            deck.Cards.Remove(link);
            db.DeckCards.Remove(link);
        }
        foreach (var (card, position) in wantedLinks)
        {
            var link = deck.Cards.FirstOrDefault(l => l.CardId == card.Id);
            if (link is null) deck.Cards.Add(new DeckCard { DeckId = deck.Id, CardId = card.Id, Card = card, Position = position });
            else link.Position = position;
        }
        var keep = wantedLinks.Select(w => w.Card.Id).ToHashSet();
        foreach (var gone in own.Values.Where(c => !keep.Contains(c.Id))) db.Cards.Remove(gone);
    }

    private void Write(Card card, GameCard content)
    {
        card.KindId = CardKind.Ids[content.KindId];
        card.Depth = content.Depth;
        card.Text = LocalizedText.From(content.Texts.ToDictionary(t => t.Key, t => t.Value.Text));

        var count = content.Texts.Values.Max(t => t.Options.Length);
        foreach (var extra in card.Options.Where(o => o.Position >= count).ToList())
        {
            card.Options.Remove(extra);
            db.CardOptions.Remove(extra);
        }
        for (var position = 0; position < count; position++)
        {
            var option = card.Options.FirstOrDefault(o => o.Position == position);
            if (option is null)
            {
                option = new CardOption { CardId = card.Id, Position = position };
                card.Options.Add(option);
            }
            var text = new LocalizedText();
            foreach (var (lang, cardText) in content.Texts)
                if (position < cardText.Options.Length) text.Set(lang, cardText.Options[position]);
            option.Text = text;
        }
    }

    private static bool Same(GameCard a, GameCard b) =>
        a.KindId == b.KindId && a.Depth == b.Depth &&
        a.Texts.Count == b.Texts.Count &&
        a.Texts.All(t => b.Texts.TryGetValue(t.Key, out var other) &&
                         other.Text == t.Value.Text && other.Options.SequenceEqual(t.Value.Options));
}
