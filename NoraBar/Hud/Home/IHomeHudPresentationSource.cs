using NoraBar.Models;

namespace NoraBar.Hud.Home;

internal interface IHomeHudPresentationSource : IDisposable
{
    HomeHudDesignVariant DesignVariant { get; }

    object ViewDataContext { get; }

    event EventHandler? PresentationInvalidated;

    void Initialize();

    void Start();

    void Stop();
}
