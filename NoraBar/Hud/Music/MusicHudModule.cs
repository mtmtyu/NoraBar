using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;
using NoraBar.Models;
using NoraBar.ViewModels;

namespace NoraBar.Hud.Music;

internal sealed class MusicHudModule : IHudModule
{
    private readonly object _syncRoot = new();
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly IMusicHudPresentationSource _source;
    private readonly Func<DesignVariant, FrameworkElement> _createView;
    private readonly Dictionary<DesignVariant, FrameworkElement> _views = [];
    private Dispatcher? _viewDispatcher;
    private TaskCompletionSource<bool> _notificationsDrained = CreateCompletedSignal();
    private int _notificationsInFlight;
    private bool _shellSubscribed;
    private bool _musicSubscribed;
    private bool _cleanupAttempted;
    private Exception? _cleanupException;
    private bool _isInitialized;
    private bool _isActive;
    private bool _isDisposed;

    internal MusicHudModule(MainViewModel viewModel)
        : this(
            new MainViewModelMusicHudPresentationSource(viewModel),
            MusicHudViewFactory.Create)
    {
    }

    internal MusicHudModule(
        IMusicHudPresentationSource source,
        Func<DesignVariant, FrameworkElement> createView)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(createView);
        _source = source;
        _createView = createView;
    }

    public string Id => BuiltInHudIds.Music;

    public HudModuleMetadata Metadata { get; } = new("Music", 0);

    public event EventHandler? PresentationInvalidated;

    public async ValueTask InitializeAsync(CancellationToken cancellationToken)
    {
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            bool removeResidualShell;
            bool removeResidualMusic;
            lock (_syncRoot)
            {
                if (_isInitialized || _isDisposed)
                {
                    return;
                }

                removeResidualShell = _shellSubscribed;
                removeResidualMusic = _musicSubscribed;
            }

            List<Exception>? residualRemovalExceptions = null;
            TryRemoveSubscription(
                removeResidualShell,
                () => _source.ShellPropertyChanged -= OnShellPropertyChanged,
                () => _shellSubscribed = false,
                ref residualRemovalExceptions);
            TryRemoveSubscription(
                removeResidualMusic,
                () => _source.MusicPropertyChanged -= OnMusicPropertyChanged,
                () => _musicSubscribed = false,
                ref residualRemovalExceptions);

            if (residualRemovalExceptions is not null)
            {
                throw new AggregateException(residualRemovalExceptions);
            }

            List<Exception>? exceptions = null;
            bool shellAddAttempted = false;
            bool musicAddAttempted = false;
            try
            {
                shellAddAttempted = true;
                _source.ShellPropertyChanged += OnShellPropertyChanged;
                musicAddAttempted = true;
                _source.MusicPropertyChanged += OnMusicPropertyChanged;
            }
            catch (Exception exception)
            {
                exceptions = [exception];
                lock (_syncRoot)
                {
                    _shellSubscribed = shellAddAttempted;
                    _musicSubscribed = musicAddAttempted;
                }

                TryRemoveSubscription(
                    musicAddAttempted,
                    () => _source.MusicPropertyChanged -= OnMusicPropertyChanged,
                    () => _musicSubscribed = false,
                    ref exceptions);
                TryRemoveSubscription(
                    shellAddAttempted,
                    () => _source.ShellPropertyChanged -= OnShellPropertyChanged,
                    () => _shellSubscribed = false,
                    ref exceptions);

                if (exceptions is { Count: > 1 })
                {
                    throw new AggregateException(exceptions);
                }

                ExceptionDispatchInfo.Capture(exception).Throw();
                throw new InvalidOperationException("イベント購読の失敗を再スローできませんでした。");
            }

            lock (_syncRoot)
            {
                _shellSubscribed = true;
                _musicSubscribed = true;
                _isInitialized = true;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public ValueTask ActivateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            if (!_isDisposed && !_isActive)
            {
                _isActive = true;
            }
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask DeactivateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            if (!_isDisposed && _isActive)
            {
                _isActive = false;
            }
        }

        return ValueTask.CompletedTask;
    }

    public FrameworkElement GetView(HudViewContext context)
    {
        Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            if (_viewDispatcher is not null && _viewDispatcher != dispatcher)
            {
                throw new InvalidOperationException("音楽HUDのViewは生成元のDispatcherから取得してください。");
            }

        }

        DesignVariant variant = MusicHudDesignVariantResolver.Resolve(_source.CurrentVariant);
        object dataContext = _source.ViewDataContext;
        FrameworkElement? view;
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _views.TryGetValue(variant, out view);
        }

        if (view is null)
        {
            FrameworkElement createdView = _createView(variant);
            if (createdView.Dispatcher != dispatcher)
            {
                throw new InvalidOperationException("音楽HUDのViewは現在のDispatcherで生成する必要があります。");
            }

            lock (_syncRoot)
            {
                ObjectDisposedException.ThrowIf(_isDisposed, this);
                if (_viewDispatcher is not null && _viewDispatcher != dispatcher)
                {
                    throw new InvalidOperationException("音楽HUDのViewは生成元のDispatcherから取得してください。");
                }

                if (!_views.TryGetValue(variant, out view))
                {
                    _viewDispatcher = dispatcher;
                    view = createdView;
                    _views.Add(variant, view);
                }
            }
        }

        if (!ReferenceEquals(view.DataContext, dataContext))
        {
            view.DataContext = dataContext;
        }

        return view;
    }

    public HudSize GetPreferredSize(HudViewContext context)
    {
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
        }

        return MusicHudLayout.Calculate(
            MusicHudDesignVariantResolver.Resolve(_source.CurrentVariant),
            _source.ShowProgressBar,
            _source.ShowLyrics,
            _source.HasMultipleSessions);
    }

    public async ValueTask DisposeAsync()
    {
        await _lifecycleGate.WaitAsync();
        List<Exception>? exceptions = null;
        bool unsubscribeShell;
        bool unsubscribeMusic;

        try
        {
            lock (_syncRoot)
            {
                if (_isDisposed
                    && !_shellSubscribed
                    && !_musicSubscribed
                    && _cleanupAttempted)
                {
                    if (_cleanupException is not null)
                    {
                        throw new AggregateException(_cleanupException);
                    }

                    return;
                }

                _isDisposed = true;
                _isActive = false;
                unsubscribeShell = _shellSubscribed;
                unsubscribeMusic = _musicSubscribed;
                _isInitialized = false;
            }

            TryRemoveSubscription(
                unsubscribeShell,
                () => _source.ShellPropertyChanged -= OnShellPropertyChanged,
                () => _shellSubscribed = false,
                ref exceptions);
            TryRemoveSubscription(
                unsubscribeMusic,
                () => _source.MusicPropertyChanged -= OnMusicPropertyChanged,
                () => _musicSubscribed = false,
                ref exceptions);

            Task notificationsDrained;
            lock (_syncRoot)
            {
                notificationsDrained = _notificationsDrained.Task;
            }

            await notificationsDrained;
            bool shouldCleanup;
            lock (_syncRoot)
            {
                shouldCleanup = !_cleanupAttempted;
                _cleanupAttempted = true;
            }

            if (shouldCleanup)
            {
                try
                {
                    _source.Cleanup();
                }
                catch (Exception exception)
                {
                    lock (_syncRoot)
                    {
                        _cleanupException = exception;
                    }

                    exceptions ??= [];
                    exceptions.Add(exception);
                }
            }
            else
            {
                lock (_syncRoot)
                {
                    if (_cleanupException is not null)
                    {
                        exceptions ??= [];
                        exceptions.Add(_cleanupException);
                    }
                }
            }

            if (exceptions is not null)
            {
                throw new AggregateException(exceptions);
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        bool affectsPresentation =
            string.IsNullOrEmpty(e.PropertyName)
            || e.PropertyName == nameof(MainViewModel.CurrentVariant)
            || e.PropertyName == nameof(MainViewModel.ShowProgressBar)
            || e.PropertyName == nameof(MainViewModel.ShowLyrics);
        RaisePresentationInvalidatedIfAffected(affectsPresentation);
    }

    private void OnMusicPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RaisePresentationInvalidatedIfAffected(
            string.IsNullOrEmpty(e.PropertyName)
            || e.PropertyName == nameof(MusicViewModel.HasMultipleSessions));
    }

    private void RaisePresentationInvalidatedIfAffected(bool affectsPresentation)
    {
        EventHandler? handler;
        lock (_syncRoot)
        {
            if (!affectsPresentation || _isDisposed)
            {
                return;
            }

            if (_notificationsInFlight++ == 0)
            {
                _notificationsDrained = new(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            }

            handler = PresentationInvalidated;
        }

        try
        {
            handler?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            lock (_syncRoot)
            {
                _notificationsInFlight--;
                if (_notificationsInFlight == 0)
                {
                    _notificationsDrained.TrySetResult(true);
                }
            }
        }
    }

    private void TryRemoveSubscription(
        bool shouldRemove,
        Action unsubscriptionAction,
        Action clearSubscriptionFlag,
        ref List<Exception>? exceptions)
    {
        if (!shouldRemove || !TryCleanupStep(unsubscriptionAction, ref exceptions))
        {
            return;
        }

        lock (_syncRoot)
        {
            clearSubscriptionFlag();
        }
    }

    private static bool TryCleanupStep(Action action, ref List<Exception>? exceptions)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception exception)
        {
            exceptions ??= [];
            exceptions.Add(exception);
            return false;
        }
    }

    private static TaskCompletionSource<bool> CreateCompletedSignal()
    {
        var signal = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        signal.SetResult(true);
        return signal;
    }
}
