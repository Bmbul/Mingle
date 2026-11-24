using Microsoft.EntityFrameworkCore;
using Mingle.DAL.Entities;

namespace Mingle.DAL;

public sealed class MingleDbContext(DbContextOptions<MingleDbContext> options) : DbContext(options)
{
    public DbSet<CardKind> CardKinds => Set<CardKind>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<Deck> Decks => Set<Deck>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<CardOption> CardOptions => Set<CardOption>();
    public DbSet<CardTopic> CardTopics => Set<CardTopic>();
    public DbSet<DeckCard> DeckCards => Set<DeckCard>();
    public DbSet<LibraryStory> Stories => Set<LibraryStory>();

    // Each table's mapping lives in Configurations/<Entity>Configuration.cs.
    protected override void OnModelCreating(ModelBuilder model) =>
        model.ApplyConfigurationsFromAssembly(typeof(MingleDbContext).Assembly);
}
