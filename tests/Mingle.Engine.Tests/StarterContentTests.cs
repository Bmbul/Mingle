namespace Mingle.Engine.Tests;

/// <summary>Checks the decks shipped in the client, so a content mistake fails the build, not a game night.</summary>
public class StarterContentTests
{
    private static List<string> FileNames() =>
        Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "data"), "*.json").Select(Path.GetFileName).OfType<string>().ToList();

    public static TheoryData<string> DeckFiles() => new(FileNames());

    private static Deck Load(string file) =>
        DeckJson.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", file)));

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
        Assert.All(deck.Cards, card => Assert.All(Langs.All, lang => Assert.True(card.Supports(lang), $"{card.Id} is missing {lang.Code()}")));
    }

    [Theory]
    [MemberData(nameof(DeckFiles))]
    public void Free_answer_cards_do_not_use_name_placeholders(string file)
    {
        // Armenian and Russian change names by grammatical case, so starter cards avoid {player} entirely.
        var deck = Load(file);
        Assert.All(deck.Cards, card => Assert.All(card.Texts.Values, t => Assert.DoesNotContain("{", t.Text)));
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
