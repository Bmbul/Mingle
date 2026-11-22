namespace Mingle.Engine;

public enum PluralCategory { One, Few, Many, Other }

/// <summary>CLDR plural rules for whole numbers in the shipped languages.</summary>
public static class Plurals
{
    public static PluralCategory For(Lang lang, int count)
    {
        var n = Math.Abs(count);
        return lang switch
        {
            Lang.En => n == 1 ? PluralCategory.One : PluralCategory.Other,
            // Armenian: 0 and 1 take the "one" form.
            Lang.Hy => n is 0 or 1 ? PluralCategory.One : PluralCategory.Other,
            // Russian: 1, 21, 31… → one; 2–4, 22–24… → few; everything else → many.
            Lang.Ru => (n % 10, n % 100) switch
            {
                (1, not 11) => PluralCategory.One,
                (>= 2 and <= 4, < 12 or > 14) => PluralCategory.Few,
                _ => PluralCategory.Many,
            },
            _ => PluralCategory.Other,
        };
    }
}
