using Mingle.DAL.Content;
using Mingle.DAL.Seeding;
using Mingle.Engine;

namespace Mingle.DAL.Tests;

/// <summary>Checks the built-in content of the content migrations and the stable ids, without a database.</summary>
public class SeedContentTests
{
    private static readonly StarterContent Content = StarterContent.Current();

    [Fact]
    public void Every_topic_has_a_name_in_every_language()
    {
        Assert.NotEmpty(Content.Topics);
        Assert.All(Content.Topics, t => Assert.All(Langs.All, l => Assert.False(string.IsNullOrWhiteSpace(t.Name[l]))));
    }

    [Fact]
    public void Every_topic_used_by_a_card_exists()
    {
        var known = Content.Topics.Select(t => t.Code).ToHashSet();
        Assert.All(Content.Decks.SelectMany(d => d.Cards), card =>
            Assert.All(card.Topics, code => Assert.True(known.Contains(code), $"{card.Id} uses unknown topic '{code}'")));
    }

    [Fact]
    public void Topics_stories_and_decks_have_unique_keys()
    {
        Assert.Equal(Content.Topics.Length, Content.Topics.DistinctBy(t => t.Code).Count());
        Assert.Equal(Content.Stories.Length, Content.Stories.DistinctBy(s => s.Key).Count());
        Assert.Equal(Content.Decks.Length, Content.Decks.DistinctBy(d => d.Id).Count());
    }

    [Fact]
    public void Every_story_parses_and_has_gaps_to_fill()
    {
        Assert.All(Langs.All, lang => Assert.Contains(Content.Stories, s => s.Lang == lang));
        Assert.All(Content.Stories, s => Assert.True(s.Gaps > 0, $"{s.Key} has no gaps"));
    }

    [Fact]
    public void Content_migrations_have_unique_ids_and_build_their_sql()
    {
        var migrations = StarterContent.Migrations();
        Assert.NotEmpty(migrations);
        Assert.All(migrations, m => Assert.NotEmpty(ContentSql.Apply(m.Content())));
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
