using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;
using GameDeck = Mingle.Engine.Deck;

namespace Mingle.DAL;

/// <summary>Where the playable decks come from.</summary>
public interface IDeckSource
{
    Task<ImmutableArray<GameDeck>> GetDecksAsync(CancellationToken ct = default);
}

/// <summary>Reads every deck, with its cards and translations, from the database.</summary>
public sealed class DbDeckSource(MingleDbContext db) : IDeckSource
{
    public async Task<ImmutableArray<GameDeck>> GetDecksAsync(CancellationToken ct = default)
    {
        var decks = await db.Decks
            .AsNoTracking()
            .Include(d => d.Names)
            .Include(d => d.Cards).ThenInclude(c => c.Card).ThenInclude(c => c.Translations)
            .OrderBy(d => d.CreatedAt).ThenBy(d => d.Id)
            .AsSplitQuery()
            .ToListAsync(ct);

        return [.. decks.Select(DeckMapping.ToGame)];
    }
}
