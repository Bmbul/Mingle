using Mingle.Engine;

namespace Mingle.DAL.Entities;

/// <summary>
/// A ready-made fill-in story in one language (see <see cref="Story"/> for the template format). Curated stories
/// come from the content migrations (<c>AddStarterStories</c>), with ids made from their keys. Table: <c>stories</c>.
/// </summary>
public sealed class LibraryStory
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Lang Lang { get; set; }

    public StoryTheme Theme { get; set; }

    public required string Title { get; set; }

    public required string Text { get; set; }

    /// <summary>How many words the players give (returning words not counted).</summary>
    public int Gaps { get; set; }

    public CardSource Source { get; set; } = CardSource.Curated;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
