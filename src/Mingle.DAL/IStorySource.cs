using Microsoft.EntityFrameworkCore;
using Mingle.Engine;

namespace Mingle.DAL;

/// <summary>A ready-made story as the game gets it.</summary>
public sealed record StoryInfo(Guid Id, string Title, string Text);

/// <summary>The ready-made fill-in stories.</summary>
public interface IStorySource
{
    /// <summary>A story in the language, preferring the theme (null: any) and length and avoiding the ones already played.</summary>
    Task<StoryInfo?> PickAsync(Lang lang, StoryTheme? theme, bool longStory, IReadOnlySet<Guid> seen, CancellationToken ct = default);
}

/// <summary>Reads the stories from the database.</summary>
public sealed class DbStorySource(MingleDbContext db) : IStorySource
{
    public async Task<StoryInfo?> PickAsync(Lang lang, StoryTheme? theme, bool longStory, IReadOnlySet<Guid> seen, CancellationToken ct = default)
    {
        var shelf = await db.Stories.AsNoTracking().Where(s => s.Lang == lang)
            .Select(s => new ShelfStory(s.Id, s.Theme, s.Gaps)).ToListAsync(ct);
        if (StoryShelf.Pick(shelf, theme, longStory, seen) is not { } pick) return null;
        var story = await db.Stories.AsNoTracking().FirstAsync(s => s.Id == pick.Id, ct);
        return new StoryInfo(story.Id, story.Title, story.Text);
    }
}
