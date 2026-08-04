namespace NoraBar.Hud.Launcher;

internal sealed record LauncherWindow(IntPtr Handle, int ProcessId, string Title, bool IsMinimized, DateTimeOffset LastActivated);

internal sealed record LauncherLaunchRequest(
    LauncherItemKind Kind,
    string Target,
    string? Arguments,
    string? WorkingDirectory,
    bool RunAsAdministrator,
    string? BrowserTarget);

internal interface ILauncherPlatform
{
    ValueTask<IReadOnlyList<LauncherWindow>> GetWindowsAsync(LauncherItem item, CancellationToken cancellationToken);
    ValueTask LaunchAsync(LauncherLaunchRequest request, CancellationToken cancellationToken);
    ValueTask FocusWindowAsync(LauncherWindow window, CancellationToken cancellationToken);
    ValueTask CloseWindowAsync(LauncherWindow window, CancellationToken cancellationToken);
    ValueTask ForceQuitAsync(int processId, CancellationToken cancellationToken);
    bool IsTargetAvailable(string target);
}

internal sealed class LauncherRuntime
{
    private readonly ILauncherPlatform _platform;

    internal LauncherRuntime(ILauncherPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(platform);
        _platform = platform;
    }

    internal async ValueTask ActivateAsync(LauncherItem item, bool requestNewInstance, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        bool shouldFocusExisting = !requestNewInstance
            && item.ExistingInstanceBehavior == LauncherExistingInstanceBehavior.FocusExisting
            && item.Kind is LauncherItemKind.Win32Application or LauncherItemKind.PackagedApplication;
        if (shouldFocusExisting)
        {
            IReadOnlyList<LauncherWindow> windows = await _platform.GetWindowsAsync(item, cancellationToken);
            LauncherWindow? mostRecent = windows.Where(window => window.Handle != IntPtr.Zero)
                .OrderByDescending(window => window.LastActivated).FirstOrDefault();
            if (mostRecent is not null)
            {
                await _platform.FocusWindowAsync(mostRecent, cancellationToken);
                return;
            }
        }

        string? browserTarget = item.BrowserTarget;
        if (item.Kind == LauncherItemKind.Url && !string.IsNullOrWhiteSpace(browserTarget) && !_platform.IsTargetAvailable(browserTarget))
        {
            browserTarget = null;
        }

        await _platform.LaunchAsync(new LauncherLaunchRequest(
            item.Kind, item.Target, item.Arguments, item.WorkingDirectory,
            item.RunAsAdministrator, browserTarget), cancellationToken);
    }

    internal ValueTask<IReadOnlyList<LauncherWindow>> GetWindowsAsync(LauncherItem item, CancellationToken cancellationToken) =>
        _platform.GetWindowsAsync(item, cancellationToken);

    internal ValueTask FocusWindowAsync(LauncherWindow window, CancellationToken cancellationToken) =>
        _platform.FocusWindowAsync(window, cancellationToken);

    internal ValueTask CloseAsync(LauncherWindow window, CancellationToken cancellationToken) =>
        _platform.CloseWindowAsync(window, cancellationToken);

    internal ValueTask ForceQuitAsync(int processId, CancellationToken cancellationToken) =>
        _platform.ForceQuitAsync(processId, cancellationToken);
}
