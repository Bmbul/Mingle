namespace Mingle.DAL.Entities;

/// <summary>Links a card to one of its topics (many-to-many). Table: <c>card_topics</c>.</summary>
public sealed class CardTopic
{
    public Guid CardId { get; set; }
    public Guid TopicId { get; set; }
    public Topic Topic { get; set; } = null!;
}
