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

        card.HasOne(c => c.Kind).WithMany().HasForeignKey(c => c.KindId).OnDelete(DeleteBehavior.Restrict);
        card.HasOne<Card>().WithMany().HasForeignKey(c => c.ForkedFromId).OnDelete(DeleteBehavior.SetNull);

        LocalizedColumns.Text(card, c => c.Text, "text", maxLength: 1000);
        card.Property(c => c.Source).HasConversion<string>().HasMaxLength(16);

        card.HasMany(c => c.Options).WithOne().HasForeignKey(o => o.CardId).OnDelete(DeleteBehavior.Cascade);
        card.HasMany(c => c.Topics).WithOne().HasForeignKey(t => t.CardId).OnDelete(DeleteBehavior.Cascade);
    }
}
