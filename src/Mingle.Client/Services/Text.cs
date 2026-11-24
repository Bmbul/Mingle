using System.Globalization;
using Microsoft.Extensions.Localization;
using Mingle.Client.Resources;
using Mingle.Engine;

namespace Mingle.Client.Services;

/// <summary>UI strings in the current language, with plural forms (keys end in _One, _Few, _Many, _Other).</summary>
public sealed class Text(IStringLocalizer<Strings> localizer)
{
    public static Lang UiLang =>
        Langs.TryParse(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, out var lang) ? lang : Lang.Hy;

    public string this[string key] => localizer[key];

    public string this[string key, params object[] args] => localizer[key, args];

    public string Plural(string key, int count)
    {
        var category = Plurals.For(UiLang, count);
        foreach (var suffix in new[] { category.ToString(), nameof(PluralCategory.Other), nameof(PluralCategory.Many) })
        {
            var value = localizer[$"{key}_{suffix}"];
            if (!value.ResourceNotFound) return string.Format(CultureInfo.CurrentCulture, value.Value, count);
        }
        return $"{key}: {count}";
    }

    public string KindName(string kindId) => this[$"Kind_{kindId}"];

    public static string LangName(Lang lang) => lang switch
    {
        Lang.Hy => "Հայերեն",
        Lang.Ru => "Русский",
        _ => "English",
    };
}
