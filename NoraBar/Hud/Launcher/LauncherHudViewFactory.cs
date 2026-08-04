using System.Windows;
using NoraBar.Views.Launcher;

namespace NoraBar.Hud.Launcher;

internal static class LauncherHudViewFactory
{
    internal static FrameworkElement Create() => new LauncherHudView();
}
