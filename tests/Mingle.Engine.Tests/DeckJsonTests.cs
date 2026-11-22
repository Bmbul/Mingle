namespace Mingle.Engine.Tests;

public class DeckJsonTests
{
    private const string Valid = """
        {
          "id": "demo",
          "name": { "en": "Demo", "ru": "Демо" },
          "cards": [
            { "id": "c1", "kind": "open", "depth": 1, "tags": ["travel"],
              "text": { "en": "Where to next?", "ru": "Куда дальше?" } },
            { "id": "c2", "kind": "wyr", "depth": 2,
              "text": { "en": "Would you rather…" },
              "options": { "en": ["Sea", "Mountains"] } }
          ]
        }
        """;

    [Fact]
    public void Parses_a_valid_deck()
    {
        var deck = DeckJson.Parse(Valid);

        Assert.Equal("demo", deck.Id);
        Assert.Equal("Демо", deck.NameFor(Lang.Ru));
        Assert.Equal("Demo", deck.NameFor(Lang.Hy)); // falls back to English
        Assert.Equal(2, deck.Cards.Length);
        Assert.Equal(["travel"], deck.Cards[0].Tags.ToArray());
        Assert.Equal(["Sea", "Mountains"], deck.Cards[1].TextFor(Lang.En).Options.ToArray());
    }

    [Theory]
    [InlineData("\"kind\": \"open\"", "\"kind\": \"riddle\"", "unknown kind")]
    [InlineData("\"depth\": 1", "\"depth\": 4", "depth must be")]
    [InlineData("\"en\": [\"Sea\", \"Mountains\"]", "\"en\": [\"Sea\"]", "exactly 2 options")]
    [InlineData("\"id\": \"c2\"", "\"id\": \"c1\"", "appears twice")]
    [InlineData("\"ru\": \"Куда дальше?\"", "\"xx\": \"Куда дальше?\"", "unknown language")]
    public void Reports_content_mistakes_with_the_card_id(string find, string replace, string expected)
    {
        var json = Valid.Replace(find, replace);
        var ex = Assert.Throws<FormatException>(() => DeckJson.Parse(json));
        Assert.Contains(expected, ex.Message);
    }

    [Fact]
    public void Writing_then_reading_gives_back_the_same_decks()
    {
        var original = DeckJson.Parse(Valid);

        var copy = Assert.Single(DeckJson.ParseMany(DeckJson.WriteMany([original])));

        Assert.Equal(original.Id, copy.Id);
        Assert.Equal(original.Names.OrderBy(n => n.Key), copy.Names.OrderBy(n => n.Key));
        Assert.Equal(original.Cards.Length, copy.Cards.Length);
        for (var i = 0; i < original.Cards.Length; i++)
        {
            var (a, b) = (original.Cards[i], copy.Cards[i]);
            Assert.Equal((a.Id, a.KindId, a.Depth), (b.Id, b.KindId, b.Depth));
            Assert.Equal(a.Tags.ToArray(), b.Tags.ToArray());
            foreach (var (lang, text) in a.Texts)
            {
                Assert.Equal(text.Text, b.Texts[lang].Text);
                Assert.Equal(text.Options.ToArray(), b.Texts[lang].Options.ToArray());
            }
        }
    }

    [Fact]
    public void Written_json_keeps_armenian_and_russian_readable()
    {
        var json = DeckJson.WriteMany([DeckJson.Parse(Valid)]);
        Assert.Contains("Куда дальше?", json);
    }
}
