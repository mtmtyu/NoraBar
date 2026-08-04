using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace NoraBar.Hud.Launcher;

internal sealed class WindowsLauncherPlatform : ILauncherPlatform
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const int SwRestore = 9;
    private const uint WmClose = 0x0010;
    private static readonly TimeSpan WindowInventoryCacheDuration = TimeSpan.FromMilliseconds(50);
    private readonly ILauncherWindowTracker _windowTracker;
    private readonly object _inventorySyncRoot = new();
    private WindowSnapshot[] _cachedWindows = [];
    private DateTimeOffset _cachedWindowsAt;

    internal WindowsLauncherPlatform(ILauncherWindowTracker windowTracker)
    {
        ArgumentNullException.ThrowIfNull(windowTracker);
        _windowTracker = windowTracker;
    }

    public ValueTask<IReadOnlyList<LauncherWindow>> GetWindowsAsync(
        LauncherItem item,
        CancellationToken cancellationToken)
    {
        WindowSnapshot[] inventory = GetWindowInventory(cancellationToken);
        IReadOnlyList<LauncherWindow> result = inventory
            .Where(snapshot => Matches(item, snapshot.Identity))
            .Select(snapshot => snapshot.Window)
            .ToArray();
        return ValueTask.FromResult(result);
    }

    private WindowSnapshot[] GetWindowInventory(CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        lock (_inventorySyncRoot)
        {
            if (now - _cachedWindowsAt <= WindowInventoryCacheDuration)
            {
                return _cachedWindows;
            }
        }

        var result = new List<WindowSnapshot>();
        bool wasCanceled = false;
        EnumWindows((windowHandle, _) =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                wasCanceled = true;
                return false;
            }
            if (!IsWindowVisible(windowHandle) || GetWindowTextLength(windowHandle) == 0) return true;
            string? identity = TryGetWindowApplicationIdentity(windowHandle);
            if (string.IsNullOrWhiteSpace(identity)) return true;
            GetWindowThreadProcessId(windowHandle, out uint processId);
            result.Add(new WindowSnapshot(identity, new LauncherWindow(
                windowHandle,
                checked((int)processId),
                GetWindowTitle(windowHandle),
                IsIconic(windowHandle),
                _windowTracker.GetLastActivated(windowHandle))));
            return true;
        }, IntPtr.Zero);
        if (wasCanceled) cancellationToken.ThrowIfCancellationRequested();

        WindowSnapshot[] snapshot = result.ToArray();
        lock (_inventorySyncRoot)
        {
            _cachedWindows = snapshot;
            _cachedWindowsAt = now;
        }
        return snapshot;
    }
    public ValueTask LaunchAsync(LauncherLaunchRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Kind == LauncherItemKind.Url
            && (!Uri.TryCreate(request.Target, UriKind.Absolute, out Uri? uri)
                || uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Launcher URLs must use HTTP or HTTPS.", nameof(request));
        }

        string fileName = request.Target;
        var startInfo = new ProcessStartInfo { UseShellExecute = true };
        if (request.Kind == LauncherItemKind.PackagedApplication
            && !fileName.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)
            && !File.Exists(fileName))
        {
            fileName = $"shell:AppsFolder\\{fileName}";
        }
        if (request.Kind == LauncherItemKind.Url && !string.IsNullOrWhiteSpace(request.BrowserTarget))
        {
            fileName = request.BrowserTarget;
            startInfo.ArgumentList.Add(request.Target);
        }
        else if (!string.IsNullOrWhiteSpace(request.Arguments))
        {
            startInfo.Arguments = request.Arguments;
        }

        startInfo.FileName = fileName;
        if (!string.IsNullOrWhiteSpace(request.WorkingDirectory))
        {
            startInfo.WorkingDirectory = request.WorkingDirectory;
        }
        if (request.RunAsAdministrator)
        {
            startInfo.Verb = "runas";
        }
        Process.Start(startInfo);
        return ValueTask.CompletedTask;
    }

    public ValueTask FocusWindowAsync(LauncherWindow window, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsCurrentWindow(window)) return ValueTask.CompletedTask;
        if (window.IsMinimized) ShowWindowAsync(window.Handle, SwRestore);
        SetForegroundWindow(window.Handle);
        return ValueTask.CompletedTask;
    }

    public ValueTask CloseWindowAsync(LauncherWindow window, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (IsCurrentWindow(window)) PostMessage(window.Handle, WmClose, IntPtr.Zero, IntPtr.Zero);
        return ValueTask.CompletedTask;
    }

    public ValueTask ForceQuitAsync(int processId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using Process process = Process.GetProcessById(processId);
        process.Kill(entireProcessTree: true);
        return ValueTask.CompletedTask;
    }

    public bool IsTargetAvailable(string target) =>
        File.Exists(target) || Directory.Exists(target);

    internal static string? TryGetWindowApplicationIdentity(IntPtr windowHandle)
    {
        GetWindowThreadProcessId(windowHandle, out uint processId);
        if (processId == 0) return null;
        IntPtr process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (process == IntPtr.Zero) return null;
        try
        {
            uint appIdLength = 0;
            int appIdResult = GetApplicationUserModelId(process, ref appIdLength, null);
            if ((appIdResult == 122 || appIdResult == 0) && appIdLength > 0)
            {
                var appId = new StringBuilder(checked((int)appIdLength));
                if (GetApplicationUserModelId(process, ref appIdLength, appId) == 0)
                {
                    return appId.ToString();
                }
            }

            var path = new StringBuilder(32768);
            int length = path.Capacity;
            return QueryFullProcessImageName(process, 0, path, ref length)
                ? path.ToString()
                : null;
        }
        finally
        {
            CloseHandle(process);
        }
    }

    private static bool IsCurrentWindow(LauncherWindow window)
    {
        if (!IsWindow(window.Handle)) return false;
        GetWindowThreadProcessId(window.Handle, out uint processId);
        return processId == window.ProcessId;
    }

    private static bool Matches(LauncherItem item, string? identity)
    {
        if (string.IsNullOrWhiteSpace(identity)) return false;
        if (item.Kind == LauncherItemKind.PackagedApplication)
        {
            return string.Equals(item.Target, identity, StringComparison.OrdinalIgnoreCase);
        }
        if (item.Kind != LauncherItemKind.Win32Application) return false;
        try
        {
            return string.Equals(Path.GetFullPath(item.Target), Path.GetFullPath(identity), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static string GetWindowTitle(IntPtr handle)
    {
        var title = new StringBuilder(GetWindowTextLength(handle) + 1);
        GetWindowText(handle, title, title.Capacity);
        return title.ToString();
    }

    private sealed record WindowSnapshot(string Identity, LauncherWindow Window);

    private delegate bool EnumWindowsDelegate(IntPtr windowHandle, IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsDelegate callback, IntPtr parameter);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr windowHandle);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr windowHandle);
    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr windowHandle);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr windowHandle, StringBuilder text, int maximumCount);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr windowHandle);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindowAsync(IntPtr windowHandle, int command);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")]
    private static extern IntPtr OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder path, ref int size);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetApplicationUserModelId(IntPtr process, ref uint applicationUserModelIdLength, StringBuilder? applicationUserModelId);
}
