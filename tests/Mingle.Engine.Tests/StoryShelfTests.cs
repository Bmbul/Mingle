namespace Mingle.Engine.Tests;

public class StoryShelfTests
{
    private static readonly ShelfStory WorkShort = new(Guid.NewGuid(), StoryTheme.Work, 6);
    private static readonly ShelfStory WorkLong = new(Guid.NewGuid(), StoryTheme.Work, 9);
    private static readonly ShelfStory LoveShort = new(Guid.NewGuid(), StoryTheme.Love, 6);
    private static readonly ShelfStory LoveLong = new(Guid.NewGuid(), StoryTheme.Love, 9);
    private static readonly ShelfStory[] Shelf = [WorkShort, WorkLong, LoveShort, LoveLong];

    private static ShelfStory? Pick(StoryTheme? theme, bool longStory, params ShelfStory[] seen) =>
        StoryShelf.Pick(Shelf, theme, longStory, seen.Select(s => s.Id).ToHashSet(), new Random(1));

    [Fact]
    public void The_theme_and_length_asked_for_come_first()
    {
        Assert.Equal(WorkShort, Pick(StoryTheme.Work, false));
        Assert.Equal(LoveLong, Pick(StoryTheme.Love, true));
    }

    [Fact]
    public void Played_stories_wait_until_the_others_are_used_up()
    {
        Assert.Equal(WorkLong, Pick(StoryTheme.Work, false, WorkShort));                              // length gives way first
        Assert.Equal(StoryTheme.Love, Pick(StoryTheme.Work, false, WorkShort, WorkLong)!.Theme);      // then the theme
        Assert.Equal(StoryTheme.Work, Pick(StoryTheme.Work, false, [.. Shelf])!.Theme);               // then a repeat
    }

    [Fact]
    public void Random_or_an_unknown_theme_means_any_theme()
    {
        Assert.Null(StoryShelf.ParseTheme("random"));
        Assert.Null(StoryShelf.ParseTheme("3"));
        Assert.Equal(StoryTheme.Fairy, StoryShelf.ParseTheme("fairy"));
        Assert.Equal(LoveShort, Pick(null, false, WorkShort));
        Assert.Null(StoryShelf.Pick([], StoryTheme.Work, false, new HashSet<Guid>()));
    }
}
