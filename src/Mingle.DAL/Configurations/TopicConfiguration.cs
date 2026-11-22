using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mingle.DAL.Entities;

namespace Mingle.DAL.Configurations;

internal sealed class TopicConfiguration : IEntityTypeConfiguration<Topic>
{
    public void Configure(EntityTypeBuilder<Topic> topic)
    {
        topic.ToTable("topics");
        topic.HasKey(t => t.Id);
        topic.Property(t => t.Code).HasMaxLength(40);
        topic.HasIndex(t => t.Code).IsUnique();
        LocalizedColumns.Text(topic, t => t.Name, "name", maxLength: 60);
        topic.Property(t => t.About).HasMaxLength(200);
        topic.Property(t => t.Audience).HasConversion<string>().HasMaxLength(16);
    }
}
