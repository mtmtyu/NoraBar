using System.Windows.Threading;
using NoraBar.Models;

namespace NoraBar.Hud.Home;

internal interface IHomeHudPresentationSource : IDisposable
{
    HomeHudDesignVariant DesignVariant { get; }

    IReadOnlyList<NoraBar.Hud.Home.Widgets.HomeWidgetConfig> ActiveWidgets { get; }

    double MaxWidgetWidth { get; }

    double MaxWidgetHeight { get; }

    object ViewDataContext { get; }

    Dispatcher? OwningDispatcher { get; }

    event EventHandler? PresentationInvalidated;

    void Initialize();

    void Start();

    void Stop();
}
