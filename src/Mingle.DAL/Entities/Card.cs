namespace Mingle.DAL.Entities;

/// <summary>
/// One canonical card. Decks reference it, so likes and stats gather in one place. Table: <c>cards</c>.
/// </summary>
public sealed class Card
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public short KindId { get; set; }
    public CardKind Kind { get; set; } = null!;

    public int Depth { get; set; }

    /// <summary>The question in each language; a null language means the card is not translated into it.</summary>
    public LocalizedText Text { get; set; } = new();

    /// <summary>Which translations a machine made and nobody has reviewed yet.</summary>
    public LocalizedFlags Machine { get; set; } = new();

    public CardSource Source { get; set; } = CardSource.Curated;
    public Guid? ForkedFromId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<CardOption> Options { get; set; } = [];
    public List<CardTopic> Topics { get; set; } = [];
}

/// <summary>Where a card came from.</summary>
public enum CardSource { Curated, Ai, User }
