using System.IO;
using System.Runtime.InteropServices;

namespace NoraBar.Hud.Launcher;

internal interface ILauncherApplicationCatalog : IDisposable
{
    Task<IReadOnlyList<LauncherItem>> GetInstalledApplicationsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<LauncherItem>> GetRunningApplicationsAsync(CancellationToken cancellationToken);
}

internal sealed class LauncherApplicationCatalog : ILauncherApplicationCatalog
{
    private readonly object _syncRoot = new();
    private Task<IReadOnlyList<LauncherItem>>? _installedCatalogTask;
    private CancellationTokenSource _disposeCancellation = new();
    private bool _isDisposed;

    public Task<IReadOnlyList<LauncherItem>> GetInstalledApplicationsAsync(CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _installedCatalogTask ??= RunStaAsync(DiscoverInstalled, _disposeCancellation.Token);
            return _installedCatalogTask.WaitAsync(cancellationToken);
        }
    }

    public Task<IReadOnlyList<LauncherItem>> GetRunningApplicationsAsync(CancellationToken cancellationToken) =>
        Task.Run<IReadOnlyList<LauncherItem>>(() =>
        {
            var items = new Dictionary<string, LauncherItem>(StringComparer.OrdinalIgnoreCase);
            foreach (System.Diagnostics.Process process in System.Diagnostics.Process.GetProcesses())
            {
                using (process)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        if (process.MainWindowHandle == IntPtr.Zero || string.IsNullOrWhiteSpace(process.MainModule?.FileName)) continue;
                        string path = process.MainModule.FileName;
                        items.TryAdd(path, new LauncherItem(
                            CreateStableId(path), process.MainWindowTitle, LauncherItemKind.Win32Application, path));
                    }
                    catch (InvalidOperationException) { }
                    catch (System.ComponentModel.Win32Exception) { }
                }
            }
            return items.Values.OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToArray();
        }, cancellationToken);

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _disposeCancellation.Cancel();
            _disposeCancellation.Dispose();
        }
    }

    private static IReadOnlyList<LauncherItem> DiscoverInstalled(CancellationToken cancellationToken)
    {
        string[] roots =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
        ];
        var items = new Dictionary<string, LauncherItem>(StringComparer.OrdinalIgnoreCase);
        foreach (string root in roots.Where(Directory.Exists))
        {
            foreach (string shortcut in Directory.EnumerateFiles(root, "*.*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true })
                .Where(path => Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase)
                    || Path.GetExtension(path).Equals(".url", StringComparison.OrdinalIgnoreCase)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                LauncherItem? item = LauncherShortcutResolver.TryCreate(shortcut);
                if (item is not null)
                {
                    string identity = $"{item.Kind}\0{item.Target}\0{item.Arguments}";
                    items.TryAdd(identity, item);
                }
            }
        }
        return items.Values.OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    private static Task<IReadOnlyList<LauncherItem>> RunStaAsync(
        Func<CancellationToken, IReadOnlyList<LauncherItem>> operation,
        CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<IReadOnlyList<LauncherItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { completion.TrySetResult(operation(cancellationToken)); }
            catch (OperationCanceledException exception) { completion.TrySetCanceled(exception.CancellationToken); }
            catch (Exception exception) { completion.TrySetException(exception); }
        }) { IsBackground = true, Name = "NoraBar Launcher Shell Catalog" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static string CreateStableId(string identity)
    {
        byte[] hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(identity.ToUpperInvariant()));
        return $"app-{Convert.ToHexString(hash.AsSpan(0, 12)).ToLowerInvariant()}";
    }
}
