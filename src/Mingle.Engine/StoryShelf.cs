namespace Mingle.Engine;

/// <summary>The theme chips of the stories game; with no theme (or "random") any of them goes.</summary>
public enum StoryTheme { Travel, Love, Work, Family, Fairy, Misc }

/// <summary>A ready-made story on the shelf, as far as choosing one needs to know.</summary>
public sealed record ShelfStory(Guid Id, StoryTheme Theme, int Gaps);

/// <summary>
/// Chooses the next ready-made story: the asked theme and length, one this phone has not played yet. When those run
/// out it gives up the length first, then the theme, and only then repeats a story.
/// </summary>
public static class StoryShelf
{
    /// <summary>A story with this many gaps or more counts as long.</summary>
    public const int LongFrom = 7;

    /// <summary>Reads a theme chip ("travel"…); "random", empty or unknown is null, meaning any theme.</summary>
    public static StoryTheme? ParseTheme(string? code) =>
        Enum.TryParse<StoryTheme>(code, ignoreCase: true, out var theme) && Enum.IsDefined(theme) && !int.TryParse(code, out _)
            ? theme : null;

    public static ShelfStory? Pick(IReadOnlyList<ShelfStory> stories, StoryTheme? theme, bool longStory, IReadOnlySet<Guid> seen, Random? random = null)
    {
        random ??= Random.Shared;
        bool Theme(ShelfStory s) => theme is null || s.Theme == theme;
        bool Length(ShelfStory s) => s.Gaps >= LongFrom == longStory;
        bool Fresh(ShelfStory s) => !seen.Contains(s.Id);

        Func<ShelfStory, bool>[] tiers =
        [
            s => Theme(s) && Length(s) && Fresh(s),
            s => Theme(s) && Fresh(s),
            s => Length(s) && Fresh(s),
            Fresh,
            s => Theme(s),
            _ => true,
        ];
        foreach (var tier in tiers)
        {
            var pool = stories.Where(tier).ToList();
            if (pool.Count > 0) return pool[random.Next(pool.Count)];
        }
        return null;
    }
}
