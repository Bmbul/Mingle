namespace Mingle.DAL.Entities;

/// <summary>
/// A named set of cards. Table: <c>decks</c>. Starter decks have no edit key; a deck made in the
/// editor stores the SHA-256 hash of its edit key, which only the device that made it holds.
/// </summary>
public sealed class Deck
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public LocalizedText Name { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Hex SHA-256 of the edit key; null for starter decks, which nobody can edit online.</summary>
    public string? EditKeyHash { get; set; }

    public bool IsUserDeck => EditKeyHash is not null;

    public List<DeckCard> Cards { get; set; } = [];
}
