namespace Mingle.DAL.Entities;

/// <summary>A deck's name in one language. Table: <c>deck_names</c>.</summary>
public sealed class DeckName
{
    public required string DeckId { get; set; }
    public required string Lang { get; set; }
    public required string Name { get; set; }
}
