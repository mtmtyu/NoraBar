using System.Diagnostics;
using System.Threading;
using System.Windows;
using NoraBar.Hud;
using NoraBar.Hud.Music;
using NoraBar.Services;
using NoraBar.ViewModels;

namespace NoraBar;

/// <summary>
/// アプリケーション全体の依存関係と終了ライフサイクルを構成します。
/// </summary>
public partial class App : Application
{
    private const string AppMutexName = "NoraBar.AppMutex";

    private readonly ShutdownTaskCoordinator _shutdownCoordinator = new();
    private Mutex? _appMutex;
    private MainViewModel? _viewModel;
    private MusicHudModule? _musicHudModule;
    private HudRegistry? _hudRegistry;
    private HudRouter? _hudRouter;
    private MainWindow? _mainWindow;

    protected override async void OnStartup(StartupEventArgs e)
    {
        _appMutex = new Mutex(false, AppMutexName);
        base.OnStartup(e);

        try
        {
            _viewModel = new MainViewModel();
            _musicHudModule = new MusicHudModule(_viewModel);
            _hudRegistry = new HudRegistry();
            _hudRegistry.Register(_musicHudModule);

            UserSettings settings = _viewModel.CurrentSettings;
            _hudRouter = new HudRouter(
                _hudRegistry,
                settings.DefaultHudId,
                settings.EnabledHudModuleIds);

            _mainWindow = new MainWindow(_viewModel, _hudRouter, RequestShutdownAsync);
            MainWindow = _mainWindow;

            await _hudRouter.InitializeAsync(CancellationToken.None);
            using (IDisposable? startupCompletion =
                   _shutdownCoordinator.TryBeginStartupCompletion())
            {
                if (!ShouldShowMainWindow(
                        startupCompletion is not null,
                        _mainWindow.IsShutdownRequested))
                {
                    return;
                }

                _mainWindow.RefreshHudPresentation();
                _mainWindow.Show();
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"NoraBarの起動に失敗しました。{Environment.NewLine}{exception.Message}",
                "NoraBar",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            await CleanupStartupFailureAsync();
            Shutdown(-1);
        }
    }

    internal static bool ShouldShowMainWindow(
        bool startupCompletionAcquired,
        bool shutdownRequested)
    {
        return startupCompletionAcquired && !shutdownRequested;
    }

    internal Task RequestShutdownAsync()
    {
        return _shutdownCoordinator.RunOnce(ShutdownCoreAsync);
    }

    private async Task ShutdownCoreAsync()
    {
        var exceptions = new List<Exception>();
        await CleanupApplicationResourcesAsync(exceptions);

        AggregateException? shutdownException = exceptions.Count > 0
            ? new AggregateException("NoraBarの終了処理中にエラーが発生しました。", exceptions)
            : null;
        if (shutdownException is not null)
        {
            Trace.TraceError(shutdownException.ToString());
        }

        Shutdown();

        if (shutdownException is not null)
        {
            throw shutdownException;
        }
    }

    private async Task CleanupStartupFailureAsync()
    {
        var ignored = new List<Exception>();
        await CleanupApplicationResourcesAsync(ignored);
    }

    private async Task CleanupApplicationResourcesAsync(ICollection<Exception> exceptions)
    {
        if (_mainWindow is not null)
        {
            Capture(_mainWindow.DetachHudRouter, exceptions);
        }

        if (_hudRouter is not null)
        {
            await CaptureAsync(
                () => _hudRouter.ShutdownAsync(CancellationToken.None),
                exceptions);
        }

        if (_hudRegistry is not null)
        {
            await CaptureAsync(
                async () => await _hudRegistry.DisposeAsync(),
                exceptions);
        }

        if (_mainWindow is not null)
        {
            Capture(_mainWindow.ReleaseShellResources, exceptions);
            Capture(_mainWindow.AllowClose, exceptions);
            Capture(_mainWindow.Close, exceptions);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _appMutex?.Dispose();
        base.OnExit(e);
    }

    private static void Capture(Action operation, ICollection<Exception> exceptions)
    {
        try
        {
            operation();
        }
        catch (Exception exception)
        {
            exceptions.Add(exception);
        }
    }

    private static async Task CaptureAsync(
        Func<Task> operation,
        ICollection<Exception> exceptions)
    {
        try
        {
            await operation();
        }
        catch (Exception exception)
        {
            exceptions.Add(exception);
        }
    }
}
