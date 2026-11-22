namespace Mingle.DAL.Entities;

/// <summary>A card's text (and options) in one language. Table: <c>card_translations</c>.</summary>
public sealed class CardTranslation
{
    public required string CardId { get; set; }
    public required string Lang { get; set; }
    public required string Text { get; set; }
    public string[] Options { get; set; } = [];
    public TranslationStatus Status { get; set; } = TranslationStatus.Original;
}

/// <summary>How far a translation can be trusted.</summary>
public enum TranslationStatus { Original, Machine, Reviewed }
