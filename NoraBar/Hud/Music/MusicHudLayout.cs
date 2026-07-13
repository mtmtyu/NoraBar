using NoraBar.Models;

namespace NoraBar.Hud.Music;

internal static class MusicHudLayout
{
    private const double MinimalWidth = 450;
    private const double MinimalHeight = 80;
    private const double MinimalHeightWithProgressBar = 106;
    private const double MinimalLyricsHeight = 24;
    private const double MinimalMultipleSessionsHeight = 12;

    private const double ProductivityWidth = 560;
    private const double ProductivityHeight = 90;
    private const double ProductivityHeightWithProgressBar = 120;
    private const double ProductivityLyricsHeight = 24;
    private const double ProductivityMultipleSessionsHeight = 16;

    private const double LyricsFocusWidth = 650;
    private const double LyricsFocusHeight = 180;

    internal static HudSize Calculate(
        DesignVariant variant,
        bool showProgressBar,
        bool showLyrics,
        bool hasMultipleSessions)
    {
        return variant switch
        {
            DesignVariant.MinimalFloatingPill => new HudSize(
                MinimalWidth,
                (showProgressBar ? MinimalHeightWithProgressBar : MinimalHeight)
                + (showLyrics ? MinimalLyricsHeight : 0)
                + (hasMultipleSessions ? MinimalMultipleSessionsHeight : 0)),
            DesignVariant.ProductivityCommandIsland => new HudSize(
                ProductivityWidth,
                (showProgressBar ? ProductivityHeightWithProgressBar : ProductivityHeight)
                + (showLyrics ? ProductivityLyricsHeight : 0)
                + (hasMultipleSessions ? ProductivityMultipleSessionsHeight : 0)),
            DesignVariant.LyricsFocusedSidebar => new HudSize(LyricsFocusWidth, LyricsFocusHeight),
            _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, "未対応の音楽HUDデザインです。")
        };
    }
}
