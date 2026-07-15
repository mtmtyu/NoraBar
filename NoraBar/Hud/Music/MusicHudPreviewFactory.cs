using System.Windows;
using NoraBar.Models;

namespace NoraBar.Hud.Music;

internal readonly record struct MusicHudPreview(
    FrameworkElement View,
    HudSize PreferredSize);

internal static class MusicHudPreviewFactory
{
    internal static MusicHudPreview Create(
        DesignVariant variant,
        bool showProgressBar,
        bool showLyrics,
        bool hasMultipleSessions,
        object dataContext)
    {
        return Create(
            variant,
            showProgressBar,
            showLyrics,
            hasMultipleSessions,
            dataContext,
            MusicHudViewFactory.Create);
    }

    internal static MusicHudPreview Create(
        DesignVariant variant,
        bool showProgressBar,
        bool showLyrics,
        bool hasMultipleSessions,
        object dataContext,
        Func<DesignVariant, FrameworkElement> createView)
    {
        ArgumentNullException.ThrowIfNull(dataContext);
        ArgumentNullException.ThrowIfNull(createView);

        DesignVariant effectiveVariant = MusicHudDesignVariantResolver.Resolve(variant);
        FrameworkElement view = createView(effectiveVariant);
        view.DataContext = dataContext;

        HudSize preferredSize = MusicHudLayout.Calculate(
            effectiveVariant,
            showProgressBar,
            showLyrics,
            hasMultipleSessions);

        return new MusicHudPreview(view, preferredSize);
    }
}
