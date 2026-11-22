using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mingle.DAL.Entities;

namespace Mingle.DAL.Configurations;

internal sealed class CardTopicConfiguration : IEntityTypeConfiguration<CardTopic>
{
    public void Configure(EntityTypeBuilder<CardTopic> link)
    {
        link.ToTable("card_topics");
        link.HasKey(l => new { l.CardId, l.TopicId });
        // "Cards about these topics" is the lookup the topics game needs, so topic_id gets its own index.
        link.HasIndex(l => l.TopicId);
        link.HasOne(l => l.Topic).WithMany().HasForeignKey(l => l.TopicId).OnDelete(DeleteBehavior.Restrict);
    }
}
