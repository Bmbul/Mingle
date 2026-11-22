namespace Mingle.Engine.Tests;

public class PluralTests
{
    [Theory]
    [InlineData(1, PluralCategory.One)]
    [InlineData(21, PluralCategory.One)]
    [InlineData(101, PluralCategory.One)]
    [InlineData(11, PluralCategory.Many)]
    [InlineData(2, PluralCategory.Few)]
    [InlineData(4, PluralCategory.Few)]
    [InlineData(22, PluralCategory.Few)]
    [InlineData(12, PluralCategory.Many)]
    [InlineData(14, PluralCategory.Many)]
    [InlineData(5, PluralCategory.Many)]
    [InlineData(0, PluralCategory.Many)]
    [InlineData(20, PluralCategory.Many)]
    public void Russian(int n, PluralCategory expected) => Assert.Equal(expected, Plurals.For(Lang.Ru, n));

    [Theory]
    [InlineData(0, PluralCategory.One)]
    [InlineData(1, PluralCategory.One)]
    [InlineData(2, PluralCategory.Other)]
    [InlineData(20, PluralCategory.Other)]
    public void Armenian(int n, PluralCategory expected) => Assert.Equal(expected, Plurals.For(Lang.Hy, n));

    [Theory]
    [InlineData(1, PluralCategory.One)]
    [InlineData(0, PluralCategory.Other)]
    [InlineData(2, PluralCategory.Other)]
    public void English(int n, PluralCategory expected) => Assert.Equal(expected, Plurals.For(Lang.En, n));
}
