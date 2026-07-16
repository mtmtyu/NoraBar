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

    private readonly ApplicationExitCoordinator _exitCoordinator;
    private Mutex? _appMutex;
    private MainViewModel? _viewModel;
    private MusicHudModule? _musicHudModule;
    private HudRegistry? _hudRegistry;
    private HudRouter? _hudRouter;
    private MainWindow? _mainWindow;

    public App()
    {
        _exitCoordinator = new ApplicationExitCoordinator(
            CleanupApplicationResourcesAsync,
            HandleApplicationExitAsync,
            exitCode => Shutdown(exitCode));
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        _appMutex = new Mutex(false, AppMutexName);
        base.OnStartup(e);

        Task<ApplicationExitResult>? startupFailureTask = null;
        using (IDisposable startupCompletion =
               _exitCoordinator.TryBeginStartupCompletion()
               ?? throw new InvalidOperationException(
                   "起動完了処理を開始できませんでした。"))
        {
            try
            {
                _viewModel = new MainViewModel();
                _musicHudModule = new MusicHudModule(_viewModel);
                _hudRegistry = new HudRegistry();
                _hudRegistry.Register(_musicHudModule);

                UserSettings settings = _viewModel.SettingsSnapshot;
                _hudRouter = new HudRouter(
                    _hudRegistry,
                    settings.DefaultHudId,
                    settings.EnabledHudModuleIds);

                _mainWindow = new MainWindow(_viewModel, _hudRouter, RequestShutdownAsync);
                MainWindow = _mainWindow;

                await _hudRouter.InitializeAsync(CancellationToken.None);
                if (!ShouldShowMainWindow(
                        startupCompletionAcquired: true,
                        _mainWindow.IsShutdownRequested))
                {
                    return;
                }

                _mainWindow.RefreshHudPresentation();
                _mainWindow.Show();
            }
            catch (Exception exception)
            {
                startupFailureTask =
                    _exitCoordinator.RequestStartupFailureAsync(exception);
            }
        }

        if (startupFailureTask is not null)
        {
            await startupFailureTask;
        }
    }

    internal static bool ShouldShowMainWindow(
        bool startupCompletionAcquired,
        bool shutdownRequested)
    {
        return startupCompletionAcquired && !shutdownRequested;
    }

    internal async Task RequestShutdownAsync()
    {
        ApplicationExitResult result =
            await _exitCoordinator.RequestShutdownAsync();
        if (result.Reason == ApplicationExitReason.NormalShutdown &&
            result.CleanupExceptions.Count > 0)
        {
            throw new AggregateException(
                "NoraBarの終了処理中にエラーが発生しました。",
                result.CleanupExceptions);
        }
    }

    private Task HandleApplicationExitAsync(ApplicationExitResult result)
    {
        if (result.Reason == ApplicationExitReason.StartupFailure)
        {
            StartupFailureReport failureReport =
                result.CreateStartupFailureReport();
            IReadOnlyList<Exception> traceFailures = failureReport.WriteTrace(
                message => Trace.TraceError(message));
            string userMessage = traceFailures.Count == 0
                ? failureReport.UserMessage
                : $"{failureReport.UserMessage}{Environment.NewLine}詳細ログの記録にも失敗しました。";

            MessageBox.Show(
                userMessage,
                "NoraBar",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return Task.CompletedTask;
        }

        if (result.CleanupExceptions.Count > 0)
        {
            var shutdownException = new AggregateException(
                "NoraBarの終了処理中にエラーが発生しました。",
                result.CleanupExceptions);
            Trace.TraceError(shutdownException.ToString());
        }

        return Task.CompletedTask;
    }

    private async Task<IReadOnlyList<Exception>> CleanupApplicationResourcesAsync()
    {
        var exceptions = new List<Exception>();

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

        return exceptions.AsReadOnly();
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
