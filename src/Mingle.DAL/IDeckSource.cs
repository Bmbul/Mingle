using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;
using GameDeck = Mingle.Engine.Deck;

namespace Mingle.DAL;

/// <summary>Where the playable decks come from.</summary>
public interface IDeckSource
{
    Task<ImmutableArray<GameDeck>> GetDecksAsync(CancellationToken ct = default);
}

/// <summary>Reads the starter decks, with their cards, options and topics, from the database.</summary>
public sealed class DbDeckSource(MingleDbContext db) : IDeckSource
{
    public async Task<ImmutableArray<GameDeck>> GetDecksAsync(CancellationToken ct = default)
    {
        var decks = await db.Decks
            .AsNoTracking()
            .Where(d => d.EditKeyHash == null) // starter decks; user decks are shared by link only
            .Include(d => d.Cards).ThenInclude(c => c.Card).ThenInclude(c => c.Options)
            .Include(d => d.Cards).ThenInclude(c => c.Card).ThenInclude(c => c.Topics).ThenInclude(t => t.Topic)
            .OrderBy(d => d.CreatedAt).ThenBy(d => d.Id)
            .AsSplitQuery()
            .ToListAsync(ct);

        return [.. decks.Select(DeckMapping.ToGame)];
    }
}
