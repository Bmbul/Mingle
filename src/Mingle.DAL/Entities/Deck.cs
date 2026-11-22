namespace Mingle.DAL.Entities;

/// <summary>A named set of cards. Table: <c>decks</c>.</summary>
public sealed class Deck
{
    public required string Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<DeckName> Names { get; set; } = [];
    public List<DeckCard> Cards { get; set; } = [];
}
