using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mingle.DAL.Entities;

namespace Mingle.DAL.Configurations;

internal sealed class CardConfiguration : IEntityTypeConfiguration<Card>
{
    public void Configure(EntityTypeBuilder<Card> card)
    {
        card.ToTable("cards", t => t.HasCheckConstraint("ck_cards_depth", "depth BETWEEN 1 AND 3"));
        card.HasKey(c => c.Id);
        card.Property(c => c.Id).HasMaxLength(64);
        card.Property(c => c.KindId).HasMaxLength(16);
        card.Property(c => c.Source).HasConversion<string>().HasMaxLength(16);
        card.Property(c => c.ForkedFromId).HasMaxLength(64);
        card.HasMany(c => c.Translations).WithOne().HasForeignKey(t => t.CardId).OnDelete(DeleteBehavior.Cascade);
        card.HasIndex(c => c.KindId);
    }
}
