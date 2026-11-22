using System.Collections.Immutable;
using Mingle.DAL.Entities;
using Mingle.Engine;
using GameCard = Mingle.Engine.Card;
using GameDeck = Mingle.Engine.Deck;

namespace Mingle.DAL;

/// <summary>Converts database entities to the game engine's decks and cards.</summary>
public static class DeckMapping
{
    /// <summary>Needs the deck's cards with their options and topics loaded.</summary>
    public static GameDeck ToGame(Entities.Deck deck) => new(
        deck.Id.ToString(),
        deck.Name.Present().ToImmutableDictionary(n => n.Lang, n => n.Value),
        [.. deck.Cards.OrderBy(c => c.Position).Select(c => ToGame(c.Card))]);

    public static GameCard ToGame(Entities.Card card)
    {
        var options = card.Options.OrderBy(o => o.Position).ToList();
        var texts = ImmutableDictionary.CreateBuilder<Lang, CardText>();

        // A language counts only when the question and every option exist in it.
        foreach (var lang in Langs.All)
        {
            if (card.Text.Get(lang) is not { } text) continue;
            var optionTexts = options.Select(o => o.Text.Get(lang)).ToList();
            if (optionTexts.Any(t => t is null)) continue;
            texts[lang] = new CardText(text, [.. optionTexts!]);
        }

        return new GameCard(
            card.Id.ToString(),
            Entities.CardKind.CodeOf(card.KindId),
            card.Depth,
            [.. card.Topics.Select(t => t.Topic.Code).Order(StringComparer.Ordinal)],
            texts.ToImmutable());
    }
}
