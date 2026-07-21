using System.Globalization;
using NoraBar.Models;

namespace NoraBar.Hud.Home;

internal static class HomeHudClockFormatter
{
    internal static string FormatTime(
        DateTimeOffset value,
        HomeHudTimeFormat format,
        CultureInfo systemCulture)
    {
        ArgumentNullException.ThrowIfNull(systemCulture);
        return format switch
        {
            HomeHudTimeFormat.System => value.ToString("t", systemCulture),
            HomeHudTimeFormat.TwelveHour => value.ToString("h:mm tt", systemCulture),
            HomeHudTimeFormat.TwentyFourHour => value.ToString("HH:mm", CultureInfo.InvariantCulture),
            _ => value.ToString("t", systemCulture)
        };
    }

    internal static string FormatDate(DateTimeOffset value, AppLanguage language)
    {
        CultureInfo culture = language == AppLanguage.Japanese
            ? CultureInfo.GetCultureInfo("ja-JP")
            : CultureInfo.GetCultureInfo("en-US");
        string format = language == AppLanguage.Japanese
            ? "yyyy年M月d日 dddd"
            : "dddd, MMMM d";
        return value.ToString(format, culture);
    }
}
