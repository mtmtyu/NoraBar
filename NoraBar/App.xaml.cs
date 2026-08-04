using System.Diagnostics;
using System.Threading;
using System.Windows;
using NoraBar.Hud;
using NoraBar.Hud.Music;
using NoraBar.Hud.Home;
using NoraBar.Hud.Launcher;
using NoraBar.Views.Launcher;
using System.Windows.Interop;
using NoraBar.Services;
using NoraBar.ViewModels;

namespace NoraBar;

/// <summary>
/// アプリケーション全体の依存関係と終了ライフサイクルを構成します。
/// </summary>
public partial class App : Application
{
    private const string AppMutexName = "NoraBar.AppMutex";
    private static readonly TimeSpan HomeHudFinalCleanupTimeout =
        TimeSpan.FromSeconds(5);

    private readonly ApplicationExitCoordinator _exitCoordinator;
    private Mutex? _appMutex;
    private MainViewModel? _viewModel;
    private MusicHudModule? _musicHudModule;
    private HomeHudModule? _homeHudModule;
    private LauncherHudModule? _launcherHudModule;
    private LauncherSettingsViewModel? _launcherSettings;
    private LauncherGlobalHotkeys? _launcherHotkeys;
    private HudNavigationViewModel? _hudNavigation;
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
                _homeHudModule = new HomeHudModule(_viewModel);
                var launcherWindowTracker = new WindowsLauncherWindowTracker();
                var launcherCatalog = new LauncherApplicationCatalog();
                var launcherUsageStore = new LauncherUsageStore();
                var launcherPlatform = new WindowsLauncherPlatform(launcherWindowTracker);
                _launcherSettings = new LauncherSettingsViewModel(
                    _viewModel.SettingsSnapshot,
                    () => SettingsService.Save(_viewModel.SettingsSnapshot),
                    launcherCatalog,
                    launcherUsageStore);
                _viewModel.AttachLauncherSettings(_launcherSettings);
                var launcherSource = new LauncherHudViewModel(
                    _launcherSettings,
                    new LauncherRuntime(launcherPlatform),
                    launcherCatalog,
                    launcherWindowTracker,
                    launcherUsageStore,
                    new LauncherIconCache(),
                    Dispatcher);
                _launcherHudModule = new LauncherHudModule(launcherSource, LauncherHudViewFactory.Create);
                _launcherSettings.EditRequested += LauncherSettings_EditRequested;
                _launcherSettings.ShortcutsChanged += LauncherSettings_ShortcutsChanged;
                _hudRegistry = new HudRegistry();
                _hudRegistry.Register(_musicHudModule);
                _hudRegistry.Register(_homeHudModule);
                _hudRegistry.Register(_launcherHudModule);

                UserSettings settings = _viewModel.SettingsSnapshot;
                _hudRouter = new HudRouter(
                    _hudRegistry,
                    settings.DefaultHudId,
                    settings.EnabledHudModuleIds);
                _hudNavigation = new HudNavigationViewModel(
                    _hudRouter,
                    _hudRegistry.Modules,
                    settings,
                    _viewModel.SelectedLanguage,
                    () => SettingsService.Save(settings));
                _viewModel.AttachHudNavigation(_hudNavigation);

                _mainWindow = new MainWindow(_viewModel, _hudRouter, RequestShutdownAsync);
                _mainWindow.SourceInitialized += MainWindow_SourceInitialized;
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

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        if (_mainWindow is null || PresentationSource.FromVisual(_mainWindow) is not HwndSource source)
        {
            return;
        }
        _launcherHotkeys = new LauncherGlobalHotkeys(source);
        _launcherHotkeys.OpenRequested += LauncherHotkeys_OpenRequested;
        _launcherHotkeys.SearchRequested += LauncherHotkeys_SearchRequested;
        LauncherHudSettings settings = LauncherHudSettingsJson.Read(_viewModel!.SettingsSnapshot);
        if (!_launcherHotkeys.TryReplace(settings.OpenShortcut, settings.SearchShortcut, out string? error))
        {
            _launcherSettings?.ReportShortcutError(LocalizeLauncherShortcutError(error));
        }
    }

    private void LauncherSettings_EditRequested(object? sender, LauncherSettingsSelectionEventArgs e) =>
        _mainWindow?.Dispatcher.Invoke(() => _mainWindow.OpenHudSettings(BuiltInHudIds.Launcher));

    private void LauncherSettings_ShortcutsChanged(object? sender, LauncherShortcutsChangedEventArgs e)
    {
        if (_launcherHotkeys is not null
            && !_launcherHotkeys.TryReplace(e.Open, e.Search, out string? error))
        {
            e.Accepted = false;
            e.Error = LocalizeLauncherShortcutError(error);
        }
    }

    private string? LocalizeLauncherShortcutError(string? error)
    {
        LauncherLocalization? strings = _launcherSettings?.Strings;
        if (strings is null) return error;
        return error switch
        {
            "Global shortcuts require at least one modifier." => strings.GlobalShortcutNeedsModifier,
            "The shortcut key is invalid." => strings.GlobalShortcutKeyInvalid,
            "The shortcut is already registered by another application." => strings.GlobalShortcutConflict,
            _ => error
        };
    }

    private void LauncherHotkeys_OpenRequested(object? sender, EventArgs e) => ObserveHotkey(OpenLauncherFromHotkeyAsync(false));
    private void LauncherHotkeys_SearchRequested(object? sender, EventArgs e) => ObserveHotkey(OpenLauncherFromHotkeyAsync(true));

    private async Task OpenLauncherFromHotkeyAsync(bool focusSearch)
    {
        if (_mainWindow is null || !await _mainWindow.TryNavigateAndExpandAsync(BuiltInHudIds.Launcher)) return;
        if (focusSearch && _launcherHudModule?.CachedView is LauncherHudView view)
        {
            view.FocusSearch();
        }
    }

    private static void ObserveHotkey(Task task) =>
        _ = task.ContinueWith(
            completed => Trace.TraceError(completed.Exception?.GetBaseException().ToString()),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);

    internal static bool ShouldShowMainWindow(
        bool startupCompletionAcquired,
        bool shutdownRequested)
    {
        return startupCompletionAcquired && !shutdownRequested;
    }

    internal async Task RequestShutdownAsync()
    {
        await _exitCoordinator.RequestShutdownAsync();
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
            Capture(_mainWindow.SuspendSettingsPreview, exceptions);
        }

        if (_mainWindow is not null)
        {
            _mainWindow.SourceInitialized -= MainWindow_SourceInitialized;
        }
        if (_launcherSettings is not null)
        {
            _launcherSettings.EditRequested -= LauncherSettings_EditRequested;
            _launcherSettings.ShortcutsChanged -= LauncherSettings_ShortcutsChanged;
        }
        if (_launcherHotkeys is not null)
        {
            _launcherHotkeys.OpenRequested -= LauncherHotkeys_OpenRequested;
            _launcherHotkeys.SearchRequested -= LauncherHotkeys_SearchRequested;
            Capture(_launcherHotkeys.Dispose, exceptions);
            _launcherHotkeys = null;
        }

        Capture(() => _hudNavigation?.Dispose(), exceptions);

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

        if (_homeHudModule is not null)
        {
            using var finalCleanupCancellation = new CancellationTokenSource(
                HomeHudFinalCleanupTimeout);
            await CaptureAsync(
                () => _homeHudModule.WaitForFinalCleanupAsync(
                    finalCleanupCancellation.Token),
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
