using Mingle.DAL.Content;

namespace Mingle.Engine.Tests;

/// <summary>Checks the built-in decks the content migrations add, so a content mistake fails the build, not a game night.</summary>
public class StarterContentTests
{
    private static readonly IReadOnlyList<Deck> Decks = StarterContent.Current().Decks;

    private static List<string> FileNames() => [.. Decks.Select(d => d.Id)];

    public static TheoryData<string> DeckFiles() => new(FileNames());

    private static Deck Load(string id) => Decks.Single(d => d.Id == id);

    [Fact]
    public void Starter_decks_are_present()
    {
        Assert.True(FileNames().Count >= 2);
    }

    [Theory]
    [MemberData(nameof(DeckFiles))]
    public void Every_card_has_all_three_languages(string file)
    {
        var deck = Load(file);
        Assert.All(Langs.All, lang => Assert.False(string.IsNullOrWhiteSpace(deck.NameFor(lang))));
        foreach (var card in deck.Cards)
        {
            // Blitz word beginnings ("Արևա…") only work in their own language: "blz-hy-…" has Armenian only, and so on.
            var only = System.Text.RegularExpressions.Regex.Match(card.Id, "^blz-(hy|ru|en)-");
            if (only.Success)
            {
                Langs.TryParse(only.Groups[1].Value, out var lang);
                Assert.True(card.Texts.Count == 1 && card.Supports(lang), $"{card.Id} should be in {lang.Code()} only");
                continue;
            }
            Assert.All(Langs.All, lang => Assert.True(card.Supports(lang), $"{card.Id} is missing {lang.Code()}"));
        }
    }

    [Theory]
    [MemberData(nameof(DeckFiles))]
    public void Only_guess_cards_name_the_player_and_every_slot_fills(string file)
    {
        // "Guess my answer" cards are about the player, with {@} (and an Armenian ending) for the name; no other
        // card uses a placeholder, and every slot must be one the game can fill.
        var deck = Load(file);
        Assert.All(deck.Cards, card =>
        {
            var guess = card.KindId == CardKinds.GuessMyAnswer.Id || card.KindId == CardKinds.RankIt.Id;
            foreach (var (lang, text) in card.Texts)
            {
                Assert.Equal(guess, PlayerSlot.In(text.Text));
                Assert.DoesNotContain("{", card.TextFor(lang, "Անի").Text);
            }
        });
    }

    [Fact]
    public void Card_ids_are_unique_across_decks()
    {
        var ids = FileNames().SelectMany(f => Load(f).Cards).Select(c => c.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Starter_decks_can_run_a_full_game_for_a_mixed_language_trio()
    {
        var decks = FileNames().Select(Load).ToList();
        var state = Game.Start(TestData.Trio, new GameSettings { CardCount = 20 }, decks, seed: 3);

        while (state.Phase != GamePhase.Ended)
        {
            state = state.Phase == GamePhase.Answering
                ? TestData.AnswerAll(state)
                : Game.Apply(state, new NextCard("ani"));
        }

        Assert.Equal(20, state.History.Count);
    }
}
