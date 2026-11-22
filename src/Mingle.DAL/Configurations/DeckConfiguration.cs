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
        deck.Property(d => d.Id).HasMaxLength(64);
        deck.HasMany(d => d.Names).WithOne().HasForeignKey(n => n.DeckId).OnDelete(DeleteBehavior.Cascade);
        deck.HasMany(d => d.Cards).WithOne().HasForeignKey(c => c.DeckId).OnDelete(DeleteBehavior.Cascade);
    }
}
