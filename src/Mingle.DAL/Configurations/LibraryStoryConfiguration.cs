using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mingle.DAL.Entities;

namespace Mingle.DAL.Configurations;

internal sealed class LibraryStoryConfiguration : IEntityTypeConfiguration<LibraryStory>
{
    public void Configure(EntityTypeBuilder<LibraryStory> story)
    {
        story.ToTable("stories");
        story.HasKey(s => s.Id);
        story.Property(s => s.Lang).HasConversion<string>().HasMaxLength(8);
        story.HasIndex(s => s.Lang);
        story.Property(s => s.Theme).HasConversion<string>().HasMaxLength(16);
        story.Property(s => s.Title).HasMaxLength(80);
        story.Property(s => s.Text).HasMaxLength(2000);
        story.Property(s => s.Source).HasConversion<string>().HasMaxLength(16);
    }
}
