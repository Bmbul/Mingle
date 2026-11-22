using Mingle.Engine;

namespace Mingle.DAL.Entities;

/// <summary>
/// A text in each of the three languages. Stored as three columns on the owning table
/// (for example <c>text_hy</c>, <c>text_ru</c>, <c>text_en</c>); a missing translation is null.
/// </summary>
public sealed class LocalizedText
{
    public string? Hy { get; set; }
    public string? Ru { get; set; }
    public string? En { get; set; }

    public string? Get(Lang lang) => lang switch
    {
        Lang.Hy => Hy,
        Lang.Ru => Ru,
        Lang.En => En,
        _ => null,
    };

    public void Set(Lang lang, string? value)
    {
        switch (lang)
        {
            case Lang.Hy: Hy = value; break;
            case Lang.Ru: Ru = value; break;
            case Lang.En: En = value; break;
        }
    }

    public static LocalizedText From(IReadOnlyDictionary<Lang, string> values)
    {
        var text = new LocalizedText();
        foreach (var (lang, value) in values) text.Set(lang, value);
        return text;
    }

    public IEnumerable<(Lang Lang, string Value)> Present() =>
        Langs.All.Select(l => (l, Get(l))).Where(p => p.Item2 is not null).Select(p => (p.l, p.Item2!));
}
