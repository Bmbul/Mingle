using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mingle.DAL.Entities;

namespace Mingle.DAL.Configurations;

/// <summary>Maps a <see cref="LocalizedText"/> or <see cref="LocalizedFlags"/> to <c>{prefix}_hy</c>, <c>{prefix}_ru</c>, <c>{prefix}_en</c>.</summary>
internal static class LocalizedColumns
{
    public static void Text<T>(EntityTypeBuilder<T> entity, Expression<Func<T, LocalizedText?>> property, string prefix, int maxLength)
        where T : class =>
        entity.ComplexProperty(property, text =>
        {
            text.Property(t => t.Hy).HasColumnName($"{prefix}_hy").HasMaxLength(maxLength);
            text.Property(t => t.Ru).HasColumnName($"{prefix}_ru").HasMaxLength(maxLength);
            text.Property(t => t.En).HasColumnName($"{prefix}_en").HasMaxLength(maxLength);
        });

    public static void Flags<T>(EntityTypeBuilder<T> entity, Expression<Func<T, LocalizedFlags?>> property, string prefix)
        where T : class =>
        entity.ComplexProperty(property, flags =>
        {
            flags.Property(f => f.Hy).HasColumnName($"{prefix}_hy");
            flags.Property(f => f.Ru).HasColumnName($"{prefix}_ru");
            flags.Property(f => f.En).HasColumnName($"{prefix}_en");
        });
}
