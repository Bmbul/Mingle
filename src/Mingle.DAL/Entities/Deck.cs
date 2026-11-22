namespace Mingle.DAL.Entities;

/// <summary>A named set of cards. Table: <c>decks</c>.</summary>
public sealed class Deck
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public LocalizedText Name { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<DeckCard> Cards { get; set; } = [];
}
