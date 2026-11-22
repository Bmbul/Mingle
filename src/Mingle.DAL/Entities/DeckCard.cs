namespace Mingle.DAL.Entities;

/// <summary>A card's place in a deck. Table: <c>deck_cards</c>.</summary>
public sealed class DeckCard
{
    public Guid DeckId { get; set; }
    public Guid CardId { get; set; }
    public int Position { get; set; }

    public Card Card { get; set; } = null!;
}
