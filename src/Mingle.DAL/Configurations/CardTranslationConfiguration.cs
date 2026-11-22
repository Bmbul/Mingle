using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mingle.DAL.Entities;

namespace Mingle.DAL.Configurations;

internal sealed class CardTranslationConfiguration : IEntityTypeConfiguration<CardTranslation>
{
    public void Configure(EntityTypeBuilder<CardTranslation> text)
    {
        text.ToTable("card_translations");
        text.HasKey(t => new { t.CardId, t.Lang });
        text.Property(t => t.Lang).HasMaxLength(2);
        text.Property(t => t.Text).HasMaxLength(1000);
        text.Property(t => t.Status).HasConversion<string>().HasMaxLength(16);
    }
}
