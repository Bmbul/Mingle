using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mingle.DAL.Entities;

namespace Mingle.DAL.Configurations;

internal sealed class CardOptionConfiguration : IEntityTypeConfiguration<CardOption>
{
    public void Configure(EntityTypeBuilder<CardOption> option)
    {
        option.ToTable("card_options");
        option.HasKey(o => o.Id);
        option.HasIndex(o => new { o.CardId, o.Position }).IsUnique();
        LocalizedColumns.Text(option, o => o.Text, "text", maxLength: 200);
    }
}
