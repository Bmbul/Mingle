using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore.Migrations;
using Mingle.DAL.Entities;
using Mingle.Engine;

namespace Mingle.DAL.Content;

/// <summary>
/// A migration that adds or changes built-in content: topics, stories and starter decks with their cards.
/// <para>
/// Every row gets a stable id from its key (<see cref="Seeding.StableId"/>), and the SQL is an upsert, so a later
/// migration that lists a card again with new text simply updates it, and an existing database (where the rows
/// are already there) gets no duplicates. A deck lists all its cards: cards dropped from a deck leave it.
/// Removing content is explicit (<see cref="ContentChange.RemovedDecks"/> and friends).
/// </para>
/// To add content, add a migration deriving from this class with <c>[DbContext]</c> and <c>[Migration]</c> attributes
/// and build its <see cref="Content"/> with <see cref="Deck"/>, <see cref="Card"/>, <see cref="Topic"/> and <see cref="Story"/>.
/// Never edit a content migration that has been deployed; change content in a new one.
/// </summary>
public abstract class ContentMigration : Migration
{
    /// <summary>What this migration adds or changes; built on demand, never cached in a static field.</summary>
    public abstract ContentChange Content();

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var sql in ContentSql.Apply(Content())) migrationBuilder.Sql(sql);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var sql in ContentSql.Undo(Content())) migrationBuilder.Sql(sql);
    }

    /// <summary>A deck with its name in each language and its cards, in order.</summary>
    protected static Engine.Deck Deck(string id, string en, string hy, string ru, params Engine.Card[] cards) =>
        new(id, ImmutableDictionary<Lang, string>.Empty.Add(Lang.En, en).Add(Lang.Hy, hy).Add(Lang.Ru, ru), [.. cards]);

    /// <summary>
    /// A card. A language left out is not offered (Blitz word beginnings exist in one language only); options are
    /// the choices of "would you rather", the odd question of Odd question and the items of Rank it.
    /// </summary>
    protected static Engine.Card Card(
        string id, string kind, int depth, string[] topics,
        string? en = null, string? hy = null, string? ru = null,
        string[]? enOptions = null, string[]? hyOptions = null, string[]? ruOptions = null)
    {
        var texts = ImmutableDictionary<Lang, CardText>.Empty;
        if (en is not null) texts = texts.Add(Lang.En, new CardText(en, [.. enOptions ?? []]));
        if (hy is not null) texts = texts.Add(Lang.Hy, new CardText(hy, [.. hyOptions ?? []]));
        if (ru is not null) texts = texts.Add(Lang.Ru, new CardText(ru, [.. ruOptions ?? []]));
        return new Engine.Card(id, kind, depth, [.. topics], texts);
    }

    /// <param name="about">What the topic covers, in English, for the AI prompt.</param>
    protected static TopicRow Topic(string code, string en, string hy, string ru,
        TopicAudience audience = TopicAudience.Everyone, bool pickable = true, string? about = null) =>
        new(code, audience, pickable, en, hy, ru, about);

    protected static StoryRow Story(string key, Lang lang, StoryTheme theme, string title, string text) =>
        new(key, lang, theme, title, text);
}

/// <summary>What one content migration adds, changes or removes.</summary>
public sealed record ContentChange
{
    public ImmutableArray<TopicRow> Topics { get; init; } = [];
    public ImmutableArray<StoryRow> Stories { get; init; } = [];
    public ImmutableArray<Engine.Deck> Decks { get; init; } = [];

    /// <summary>Starter decks to delete, by id. Their cards stay unless listed in <see cref="RemovedCards"/>.</summary>
    public ImmutableArray<string> RemovedDecks { get; init; } = [];

    /// <summary>Cards to delete, by id: they leave every starter deck (a player's deck that still uses one keeps it).</summary>
    public ImmutableArray<string> RemovedCards { get; init; } = [];

    public ImmutableArray<string> RemovedStories { get; init; } = [];
}

/// <summary>A built-in topic.</summary>
public sealed record TopicRow(string Code, TopicAudience Audience, bool Pickable, string En, string Hy, string Ru, string? About)
{
    public IReadOnlyDictionary<Lang, string> Name => new Dictionary<Lang, string> { [Lang.En] = En, [Lang.Hy] = Hy, [Lang.Ru] = Ru };
}

/// <summary>A built-in story; the key gives its stable id.</summary>
public sealed record StoryRow(string Key, Lang Lang, StoryTheme Theme, string Title, string Text)
{
    public Guid Id => Seeding.StableId.For("story", Key);

    /// <summary>How many words the players give, counted with two players.</summary>
    public int Gaps => Story.Parse(Title, Text, ["A", "B"]).Gaps.Length;
}
