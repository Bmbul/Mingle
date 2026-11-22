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
}
