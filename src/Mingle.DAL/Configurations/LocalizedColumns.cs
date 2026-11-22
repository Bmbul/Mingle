using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mingle.DAL.Entities;

namespace Mingle.DAL.Configurations;

/// <summary>Maps a <see cref="LocalizedText"/> to <c>{prefix}_hy</c>, <c>{prefix}_ru</c>, <c>{prefix}_en</c>.</summary>
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
}
