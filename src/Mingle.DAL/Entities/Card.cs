namespace Mingle.DAL.Entities;

/// <summary>
/// One canonical card. Decks reference it, so likes and stats gather in one place. Table: <c>cards</c>.
/// </summary>
public sealed class Card
{
    public required string Id { get; set; }
    public required string KindId { get; set; }
    public int Depth { get; set; }
    public string[] Tags { get; set; } = [];
    public CardSource Source { get; set; } = CardSource.Curated;
    public string? ForkedFromId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<CardTranslation> Translations { get; set; } = [];
}

/// <summary>Where a card came from.</summary>
public enum CardSource { Curated, Ai, User }
