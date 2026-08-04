using NoraBar.Hud.Launcher;
using Xunit;

namespace NoraBar.Tests.Hud.Launcher;

public sealed class LauncherRuntimeTests
{
    [Fact]
    public async Task ActivateAsync_FocusesMostRecentExistingWindowAndRestoresIt()
    {
        var platform = new FakeLauncherPlatform
        {
            Windows =
            [
                new LauncherWindow(new IntPtr(1), 10, "Old", false, DateTimeOffset.UtcNow.AddMinutes(-2)),
                new LauncherWindow(new IntPtr(2), 10, "Recent", true, DateTimeOffset.UtcNow)
            ]
        };
        var runtime = new LauncherRuntime(platform);

        await runtime.ActivateAsync(ApplicationItem(), requestNewInstance: false, CancellationToken.None);

        Assert.Equal(new IntPtr(2), platform.FocusedWindow);
        Assert.True(platform.RestoreRequested);
        Assert.Null(platform.LaunchRequest);
    }

    [Fact]
    public async Task ActivateAsync_ShiftClickLaunchesNewInstance()
    {
        var platform = new FakeLauncherPlatform { Windows = [new LauncherWindow(new IntPtr(1), 1, "Existing", false, DateTimeOffset.UtcNow)] };
        var runtime = new LauncherRuntime(platform);

        await runtime.ActivateAsync(ApplicationItem(), requestNewInstance: true, CancellationToken.None);

        Assert.NotNull(platform.LaunchRequest);
        Assert.Null(platform.FocusedWindow);
    }

    [Fact]
    public async Task ActivateAsync_SupportsAdministratorUrlAndCustomBrowserFallback()
    {
        var platform = new FakeLauncherPlatform { SpecificBrowserAvailable = false };
        var runtime = new LauncherRuntime(platform);
        LauncherItem url = new("url", "Docs", LauncherItemKind.Url, "https://example.com",
            RunAsAdministrator: true, BrowserTarget: @"C:\Missing\browser.exe");

        await runtime.ActivateAsync(url, requestNewInstance: false, CancellationToken.None);

        Assert.Equal("https://example.com", platform.LaunchRequest?.Target);
        Assert.Null(platform.LaunchRequest?.BrowserTarget);
        Assert.True(platform.LaunchRequest?.RunAsAdministrator);
    }

    [Fact]
    public async Task FocusWindowAsync_UsesSelectedWindow()
    {
        var platform = new FakeLauncherPlatform();
        var runtime = new LauncherRuntime(platform);
        var selected = new LauncherWindow(new IntPtr(7), 70, "Selected", false, DateTimeOffset.MinValue);

        await runtime.FocusWindowAsync(selected, CancellationToken.None);

        Assert.Equal(selected.Handle, platform.FocusedWindow);
    }

    [Fact]
    public async Task CloseAndForceQuit_UseSelectedWindowBoundaries()
    {
        var platform = new FakeLauncherPlatform();
        var runtime = new LauncherRuntime(platform);
        var window = new LauncherWindow(new IntPtr(4), 44, "Window", false, DateTimeOffset.UtcNow);

        await runtime.CloseAsync(window, CancellationToken.None);
        await runtime.ForceQuitAsync(window.ProcessId, CancellationToken.None);

        Assert.Equal(window.Handle, platform.ClosedWindow);
        Assert.Equal(44, platform.ForceQuitProcessId);
    }

    private static LauncherItem ApplicationItem() => new(
        "app", "Application", LauncherItemKind.Win32Application, @"C:\App\app.exe");

    private sealed class FakeLauncherPlatform : ILauncherPlatform
    {
        public IReadOnlyList<LauncherWindow> Windows { get; init; } = [];
        public LauncherLaunchRequest? LaunchRequest { get; private set; }
        public IntPtr? FocusedWindow { get; private set; }
        public bool RestoreRequested { get; private set; }
        public IntPtr? ClosedWindow { get; private set; }
        public int? ForceQuitProcessId { get; private set; }
        public bool SpecificBrowserAvailable { get; init; } = true;

        public ValueTask<IReadOnlyList<LauncherWindow>> GetWindowsAsync(LauncherItem item, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Windows);

        public ValueTask LaunchAsync(LauncherLaunchRequest request, CancellationToken cancellationToken)
        {
            LaunchRequest = request;
            return ValueTask.CompletedTask;
        }

        public ValueTask FocusWindowAsync(LauncherWindow window, CancellationToken cancellationToken)
        {
            FocusedWindow = window.Handle;
            RestoreRequested = window.IsMinimized;
            return ValueTask.CompletedTask;
        }

        public ValueTask CloseWindowAsync(LauncherWindow window, CancellationToken cancellationToken)
        {
            ClosedWindow = window.Handle;
            return ValueTask.CompletedTask;
        }

        public ValueTask ForceQuitAsync(int processId, CancellationToken cancellationToken)
        {
            ForceQuitProcessId = processId;
            return ValueTask.CompletedTask;
        }

        public bool IsTargetAvailable(string target) => SpecificBrowserAvailable;
    }
}
