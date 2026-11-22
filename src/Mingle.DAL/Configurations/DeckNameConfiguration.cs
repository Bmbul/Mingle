using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mingle.DAL.Entities;

namespace Mingle.DAL.Configurations;

internal sealed class DeckNameConfiguration : IEntityTypeConfiguration<DeckName>
{
    public void Configure(EntityTypeBuilder<DeckName> name)
    {
        name.ToTable("deck_names");
        name.HasKey(n => new { n.DeckId, n.Lang });
        name.Property(n => n.Lang).HasMaxLength(2);
        name.Property(n => n.Name).HasMaxLength(100);
    }
}
