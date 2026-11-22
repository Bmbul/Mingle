using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mingle.DAL.Entities;

namespace Mingle.DAL.Configurations;

internal sealed class CardKindConfiguration : IEntityTypeConfiguration<CardKind>
{
    public void Configure(EntityTypeBuilder<CardKind> kind)
    {
        kind.ToTable("card_kinds");
        kind.HasKey(k => k.Id);
        kind.Property(k => k.Id).ValueGeneratedNever();
        kind.Property(k => k.Code).HasMaxLength(16);
        kind.HasIndex(k => k.Code).IsUnique();
        kind.Property(k => k.ResponseType).HasMaxLength(16);
        kind.Property(k => k.Audience).HasMaxLength(24);

        // The kinds come from the engine and are created by the migration itself.
        kind.HasData(CardKind.All());
    }
}
