namespace Mingle.DAL.Entities;

/// <summary>
/// What a card is about (travel, childhood…). Cards are tagged with topics, and in the
/// "Our topics" game players pick topics from this list. Table: <c>topics</c>.
/// </summary>
public sealed class Topic
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Stable short name, e.g. "travel"; used by seed files and the AI prompt.</summary>
    public required string Code { get; set; }

    public LocalizedText Name { get; set; } = new();

    /// <summary>Whether players can be offered this topic (internal ones like "us" are not).</summary>
    public bool Pickable { get; set; } = true;

    /// <summary>What the topic covers, in English, so the AI reads it as meant (e.g. "dreams" = hopes, not sleep).</summary>
    public string? About { get; set; }

    public TopicAudience Audience { get; set; } = TopicAudience.Everyone;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Who a topic suits, so couple topics are not offered to a group of friends.</summary>
public enum TopicAudience { Everyone, Friends, Couples }
