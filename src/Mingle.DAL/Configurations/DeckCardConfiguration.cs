using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mingle.DAL.Entities;

namespace Mingle.DAL.Configurations;

internal sealed class DeckCardConfiguration : IEntityTypeConfiguration<DeckCard>
{
    public void Configure(EntityTypeBuilder<DeckCard> link)
    {
        link.ToTable("deck_cards");
        link.HasKey(l => new { l.DeckId, l.CardId });
        link.HasOne(l => l.Card).WithMany().HasForeignKey(l => l.CardId).OnDelete(DeleteBehavior.Restrict);
    }
}
