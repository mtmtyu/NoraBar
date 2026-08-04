namespace NoraBar.Hud.Launcher;

internal interface ILauncherHudPresentationSource : IDisposable
{
    object ViewDataContext { get; }
    event EventHandler? PresentationInvalidated;
    void Initialize();
    void Start();
    void Stop();
}
