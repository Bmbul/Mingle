using Mingle.DAL.Seeding;
using Mingle.Engine;

namespace Mingle.DAL.Tests;

/// <summary>Checks the seed files and the stable ids without a database.</summary>
public class SeedContentTests
{
    private static string Seed(params string[] parts) => Path.Combine([DeckSeeder.DefaultFolder, .. parts]);

    private static IReadOnlyList<TopicSeed> Topics() => TopicSeed.Parse(File.ReadAllText(Seed("topics.json")));

    private static IEnumerable<Deck> Decks() =>
        Directory.GetFiles(Seed("Decks"), "*.json").Select(f => DeckJson.Parse(File.ReadAllText(f)));

    [Fact]
    public void Every_topic_has_a_name_in_every_language()
    {
        Assert.All(Topics(), t => Assert.All(Langs.All, l => Assert.False(string.IsNullOrWhiteSpace(t.Name[l]))));
    }

    [Fact]
    public void Every_topic_used_by_a_card_exists()
    {
        var known = Topics().Select(t => t.Code).ToHashSet();
        Assert.All(Decks().SelectMany(d => d.Cards), card =>
            Assert.All(card.Topics, code => Assert.True(known.Contains(code), $"{card.Id} uses unknown topic '{code}'")));
    }

    [Fact]
    public void Stable_ids_never_change_and_are_version_5()
    {
        var id = StableId.For("card", "ice-01");

        Assert.Equal(id, StableId.For("card", "ice-01"));
        Assert.NotEqual(id, StableId.For("deck", "ice-01"));
        Assert.Equal(5, id.Version);
    }
}
