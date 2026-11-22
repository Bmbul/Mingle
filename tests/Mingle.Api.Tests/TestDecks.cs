using System.Collections.Immutable;
using Mingle.DAL;
using Mingle.Engine;

namespace Mingle.Api.Tests;

/// <summary>A small in-memory deck, so room and hub tests need no database.</summary>
internal sealed class TestDecks : IDeckSource
{
    public static readonly Deck Deck = new(
        "test",
        ImmutableDictionary<Lang, string>.Empty.Add(Lang.En, "Test"),
        [.. Enumerable.Range(1, 10).Select(i => new Card(
            $"finish-{i}", "finish", 1, [],
            Langs.All.ToImmutableDictionary(l => l, l => new CardText($"Card {i} in {l.Code()}"))))]);

    public static readonly ImmutableArray<Deck> All = [Deck];

    public Task<ImmutableArray<Deck>> GetDecksAsync(CancellationToken ct = default) => Task.FromResult(All);
}

/// <summary>A few topics, so "Our topics" rooms need no database.</summary>
internal sealed class TestTopics : ITopicSource
{
    public static readonly ImmutableArray<TopicInfo> All =
        [.. Enumerable.Range(1, 12).Select(i => new TopicInfo($"topic-{i:00}",
            Langs.All.ToImmutableDictionary(l => l, l => $"Topic {i} in {l.Code()}")))];

    public Task<ImmutableArray<TopicInfo>> GetPickableTopicsAsync(CancellationToken ct = default) => Task.FromResult(All);
}

/// <summary>A clock the tests move by hand.</summary>
internal sealed class ManualClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
