using System.Collections.Immutable;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Mingle.DAL.Content;

/// <summary>
/// The built-in content as the content migrations leave it, worked out without a database: each migration in
/// order adds or replaces rows by key and removes what it lists. Tests use it to check the content.
/// </summary>
public sealed record StarterContent(ImmutableArray<TopicRow> Topics, ImmutableArray<StoryRow> Stories, ImmutableArray<Engine.Deck> Decks)
{
    /// <summary>The content migrations, oldest first.</summary>
    public static IReadOnlyList<ContentMigration> Migrations() =>
        [.. typeof(ContentMigration).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(ContentMigration)) && !t.IsAbstract)
            .Select(t => (Id: t.GetCustomAttribute<MigrationAttribute>()?.Id ?? throw new InvalidOperationException($"{t.Name} has no [Migration]."), Type: t))
            .OrderBy(m => m.Id, StringComparer.Ordinal)
            .Select(m => (ContentMigration)Activator.CreateInstance(m.Type)!)];

    public static StarterContent Current()
    {
        var topics = new Dictionary<string, TopicRow>();
        var stories = new Dictionary<string, StoryRow>();
        var decks = new Dictionary<string, Engine.Deck>();
        foreach (var migration in Migrations())
        {
            var change = migration.Content();
            foreach (var t in change.Topics) topics[t.Code] = t;
            foreach (var s in change.Stories) stories[s.Key] = s;
            foreach (var d in change.Decks) decks[d.Id] = d;
            foreach (var id in change.RemovedDecks) decks.Remove(id);
            foreach (var id in change.RemovedStories) stories.Remove(id);

            // A card listed again anywhere is updated in every deck that holds it; a removed card leaves them all.
            var updated = change.Decks.SelectMany(d => d.Cards).DistinctBy(c => c.Id).ToDictionary(c => c.Id);
            var removed = change.RemovedCards.ToHashSet();
            foreach (var (id, deck) in decks.ToList())
                decks[id] = deck with { Cards = [.. deck.Cards.Where(c => !removed.Contains(c.Id)).Select(c => updated.GetValueOrDefault(c.Id, c))] };
        }
        return new([.. topics.Values], [.. stories.Values], [.. decks.Values]);
    }
}
