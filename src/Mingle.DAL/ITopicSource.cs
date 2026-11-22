using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;
using Mingle.Engine;

namespace Mingle.DAL;

/// <summary>The topics players can be offered in the "Our topics" game.</summary>
public interface ITopicSource
{
    Task<ImmutableArray<TopicInfo>> GetPickableTopicsAsync(CancellationToken ct = default);
}

/// <summary>Reads the pickable topics from the database.</summary>
public sealed class DbTopicSource(MingleDbContext db) : ITopicSource
{
    public async Task<ImmutableArray<TopicInfo>> GetPickableTopicsAsync(CancellationToken ct = default)
    {
        var topics = await db.Topics
            .AsNoTracking()
            .Where(t => t.Pickable)
            .OrderBy(t => t.Code)
            .ToListAsync(ct);

        return [.. topics.Select(t => new TopicInfo(t.Code, t.Name.Present().ToImmutableDictionary(n => n.Lang, n => n.Value)))];
    }
}
