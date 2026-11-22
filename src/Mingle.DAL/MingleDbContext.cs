using Microsoft.EntityFrameworkCore;
using Mingle.DAL.Entities;

namespace Mingle.DAL;

public sealed class MingleDbContext(DbContextOptions<MingleDbContext> options) : DbContext(options)
{
    public DbSet<Deck> Decks => Set<Deck>();
    public DbSet<DeckName> DeckNames => Set<DeckName>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<CardTranslation> CardTranslations => Set<CardTranslation>();
    public DbSet<DeckCard> DeckCards => Set<DeckCard>();

    // Each table's mapping lives in Configurations/<Entity>Configuration.cs.
    protected override void OnModelCreating(ModelBuilder model) =>
        model.ApplyConfigurationsFromAssembly(typeof(MingleDbContext).Assembly);
}
