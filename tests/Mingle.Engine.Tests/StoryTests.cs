namespace Mingle.Engine.Tests;

public class StoryTests
{
    [Fact]
    public void Gaps_and_player_names_are_read_from_the_template()
    {
        var story = Story.Parse("Trip", "{@} and {@} went to {city}. They ate {food}.", ["Ani", "Arman"]);

        Assert.Equal(["city", "food"], story.Gaps.Select(g => g.Hint!).ToArray());
        Assert.Equal("Ani and Arman went to [city]. They ate [food].", story.Fill([]));
        Assert.Equal("Ani and Arman went to Gyumri. They ate dolma.", story.Fill(["Gyumri", " dolma "]));
    }

    [Fact]
    public void Player_names_take_turns_and_without_players_become_gaps()
    {
        Assert.Equal("Ani, Arman, Ani: [x]", Story.Parse("t", "{@}, {@}, {@}: {x}", ["Ani", "Arman"]).Fill([]));
        Assert.Equal(["name", "x"], Story.Parse("t", "{@} {x}", []).Gaps.Select(g => g.Hint!).ToArray());
    }

    [Theory]
    [InlineData("No gaps at all.")]
    [InlineData("Broken {gap")]
    [InlineData("Broken gap}")]
    [InlineData("Empty {} gap")]
    [InlineData("Nested {a {b}} gap")]
    public void Malformed_templates_are_rejected(string template)
    {
        Assert.Throws<FormatException>(() => Story.Parse("t", template, ["Ani"]));
    }

    [Fact]
    public void Gaps_are_counted_without_the_name_slots()
    {
        Assert.Equal(2, Story.CountGaps("{@} saw a {animal} and a { food }."));
    }

    [Fact]
    public void A_name_slot_in_the_title_gets_the_first_players_name()
    {
        Assert.Equal("Chef Ani", Story.Parse("Chef {@}", "{@} cooked {food}.", ["Ani", "Arman"]).Title);
    }
}
