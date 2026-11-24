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

    [Theory]
    [InlineData("Սմբատ", "ը", "որոշեց", "Սմբատը")]
    [InlineData("Անի", "ը", "որոշեց", "Անին")]
    [InlineData("Սմբատ", "ը", "էլ եկավ", "Սմբատն")]   // before a vowel
    [InlineData("Սմբատ", "ի", "", "Սմբատի")]
    [InlineData("Անի", "ի", "", "Անիի")]
    [InlineData("Աննա", "ի", "", "Աննայի")]
    [InlineData("Աննա", "ին", "", "Աննային")]
    [InlineData("Սմբատ", "ին", "", "Սմբատին")]
    [InlineData("Սմբատ", "ից", "", "Սմբատից")]
    [InlineData("Ani", "ը", "", "Ani-ն")]
    [InlineData("Սմբատ", "", "", "Սմբատ")]
    public void Armenian_names_get_the_right_ending(string name, string ending, string next, string expected)
    {
        Assert.Equal(expected, ArmenianNames.WithEnding(name, ending, next));
    }

    [Fact]
    public void Name_slots_with_endings_are_filled_in_the_story()
    {
        var story = Story.Parse("{@ի} ճաշը", "{@ը} և {@ը} որոշեցին ճաշ եփել։ Տատիկը {@ին} տվեց {ուտելիք}։", ["Սմբատ", "Անի"]);

        Assert.Equal("Սմբատի ճաշը", story.Title);
        Assert.Equal("Սմբատը և Անին որոշեցին ճաշ եփել։ Տատիկը Սմբատին տվեց [ուտելիք]։", story.Fill([]));
        Assert.Equal(1, Story.CountGaps("{@ը} և {@ին} տվեց {ուտելիք}"));
    }

    [Fact]
    public void A_marked_word_comes_back_later_without_being_asked_again()
    {
        var story = Story.Parse("Zoo", "{@} bought a {animal#1}. The {#1} ate {food}. Now the {#1} is the boss.", ["Ani"]);

        Assert.Equal(["animal", "food"], story.Gaps.Select(g => g.Hint!).ToArray());
        Assert.Equal(4, story.WordSlots);
        Assert.Equal("Ani bought a goat. The goat ate pizza. Now the goat is the boss.", story.Fill(["goat", "pizza"]));
        Assert.Equal(2, Story.CountGaps("{@} bought a {animal#1}. The {#1} ate {food}."));
    }

    [Theory]
    [InlineData("The {#1} came first. Then a {animal#1}.")]   // a repeat before its gap
    [InlineData("A {animal#1} and a {food#1}.")]               // the same mark twice
    public void Bad_word_marks_are_rejected(string template)
    {
        Assert.Throws<FormatException>(() => Story.Parse("t", template, ["Ani"]));
    }
}
