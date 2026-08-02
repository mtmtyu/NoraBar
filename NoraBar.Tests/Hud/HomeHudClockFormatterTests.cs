using System.Globalization;
using NoraBar.Hud.Home;
using NoraBar.Models;
using Xunit;

namespace NoraBar.Tests.Hud;

public sealed class HomeHudClockFormatterTests
{
    [Theory]
    [InlineData(HomeHudTimeFormat.TwelveHour, "1:05 PM")]
    [InlineData(HomeHudTimeFormat.TwentyFourHour, "13:05")]
    public void FormatTime_UsesExplicitFormat(HomeHudTimeFormat format, string expected)
    {
        var value = new DateTimeOffset(2026, 7, 21, 13, 5, 0, TimeSpan.Zero);

        string result = HomeHudClockFormatter.FormatTime(value, format, CultureInfo.GetCultureInfo("en-US"));

        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatDate_UsesApplicationLanguageCulture()
    {
        var value = new DateTimeOffset(2026, 7, 21, 13, 5, 0, TimeSpan.Zero);

        string result = HomeHudClockFormatter.FormatDate(value, AppLanguage.Japanese);

        Assert.Contains("2026年7月21日", result, StringComparison.Ordinal);
    }
}
