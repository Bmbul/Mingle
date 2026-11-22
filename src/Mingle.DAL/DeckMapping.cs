using System.Collections.Immutable;
using Mingle.DAL.Entities;
using Mingle.Engine;
using GameCard = Mingle.Engine.Card;
using GameDeck = Mingle.Engine.Deck;

namespace Mingle.DAL;

/// <summary>Converts between database entities and the game engine's decks and cards.</summary>
public static class DeckMapping
{
    public static GameDeck ToGame(Entities.Deck deck) => new(
        deck.Id,
        deck.Names.ToImmutableDictionary(n => ParseLang(n.Lang), n => n.Name),
        [.. deck.Cards.OrderBy(c => c.Position).Select(c => ToGame(c.Card))]);

    public static GameCard ToGame(Entities.Card card) => new(
        card.Id,
        card.KindId,
        card.Depth,
        [.. card.Tags],
        card.Translations.ToImmutableDictionary(
            t => ParseLang(t.Lang),
            t => new CardText(t.Text, [.. t.Options])));

    /// <summary>
    /// Builds entities for a deck. Cards already built (by id) are reused, so a card shared by
    /// several decks is stored once.
    /// </summary>
    public static Entities.Deck ToEntity(GameDeck deck, IDictionary<string, Entities.Card> knownCards) => new()
    {
        Id = deck.Id,
        Names = [.. deck.Names.Select(n => new DeckName { DeckId = deck.Id, Lang = n.Key.Code(), Name = n.Value })],
        Cards = [.. deck.Cards.Select((card, index) => new DeckCard
        {
            DeckId = deck.Id,
            CardId = card.Id,
            Position = index,
            Card = knownCards.TryGetValue(card.Id, out var known) ? known : knownCards[card.Id] = ToEntity(card),
        })],
    };

    public static Entities.Card ToEntity(GameCard card) => new()
    {
        Id = card.Id,
        KindId = card.KindId,
        Depth = card.Depth,
        Tags = [.. card.Tags],
        Translations = [.. card.Texts.Select(t => new CardTranslation
        {
            CardId = card.Id,
            Lang = t.Key.Code(),
            Text = t.Value.Text,
            Options = [.. t.Value.Options],
        })],
    };

    private static Lang ParseLang(string code) =>
        Langs.TryParse(code, out var lang) ? lang : throw new InvalidDataException($"Unknown language '{code}' in the database.");
}
