namespace Mingle.DAL.Entities;

/// <summary>
/// One answer option of a card ("Live by the sea"), with its text in each language.
/// Options have their own ids, so answers and statistics can point at them. Table: <c>card_options</c>.
/// </summary>
public sealed class CardOption
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CardId { get; set; }
    public int Position { get; set; }
    public LocalizedText Text { get; set; } = new();
}
