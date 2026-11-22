using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mingle.DAL.Entities;

namespace Mingle.DAL.Configurations;

internal sealed class DeckConfiguration : IEntityTypeConfiguration<Deck>
{
    public void Configure(EntityTypeBuilder<Deck> deck)
    {
        deck.ToTable("decks");
        deck.HasKey(d => d.Id);
        LocalizedColumns.Text(deck, d => d.Name, "name", maxLength: 100);
        deck.HasMany(d => d.Cards).WithOne().HasForeignKey(c => c.DeckId).OnDelete(DeleteBehavior.Cascade);
    }
}
