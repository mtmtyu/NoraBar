using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;
using NoraBar.Models;
using NoraBar.ViewModels;

namespace NoraBar.Hud.Home;

internal sealed class HomeHudModule : IHudModule
{
    private readonly object _syncRoot = new();
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly IHomeHudPresentationSource _source;
    private readonly Func<HomeHudDesignVariant, FrameworkElement> _createView;
    private readonly Dictionary<HomeHudDesignVariant, FrameworkElement> _views = [];
    private Dispatcher? _viewDispatcher;
    private Task? _disposeTask;
    private bool _isInitialized;
    private bool _isActive;
    private bool _isDisposing;
    private bool _isDisposed;

    internal HomeHudModule(MainViewModel viewModel)
        : this(new HomeHudViewModel(viewModel), HomeHudViewFactory.Create)
    {
    }

    internal HomeHudModule(
        IHomeHudPresentationSource source,
        Func<HomeHudDesignVariant, FrameworkElement> createView)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(createView);
        _source = source;
        _createView = createView;
    }

    public string Id => BuiltInHudIds.Home;

    public HudModuleMetadata Metadata { get; } = new("Home", 1);

    public event EventHandler? PresentationInvalidated;

    public async ValueTask InitializeAsync(CancellationToken cancellationToken)
    {
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized || IsUnavailable())
            {
                return;
            }

            _source.Initialize();
            _source.PresentationInvalidated += Source_PresentationInvalidated;
            _isInitialized = true;
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask ActivateAsync(CancellationToken cancellationToken)
    {
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            if (_isActive || IsUnavailable())
            {
                return;
            }

            _source.Start();
            _isActive = true;
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask DeactivateAsync(CancellationToken cancellationToken)
    {
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            if (!_isActive || IsUnavailable())
            {
                return;
            }

            _source.Stop();
            _isActive = false;
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public FrameworkElement GetView(HudViewContext context)
    {
        Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
        HomeHudDesignVariant variant;
        lock (_syncRoot)
        {
            ThrowIfUnavailable();
            EnsureViewDispatcher(dispatcher);

            variant = ResolveVariant(_source.DesignVariant);
            if (_views.TryGetValue(variant, out FrameworkElement? cached))
            {
                cached.DataContext = _source.ViewDataContext;
                return cached;
            }
        }

        FrameworkElement created = _createView(variant);
        if (created.Dispatcher != dispatcher)
        {
            throw new InvalidOperationException(
                "ホームHUDのViewは現在のDispatcherで生成する必要があります。");
        }

        FrameworkElement? selected = null;
        Exception? rejectionException = null;
        lock (_syncRoot)
        {
            if (_isDisposing || _isDisposed)
            {
                rejectionException = new ObjectDisposedException(nameof(HomeHudModule));
            }
            else if (_viewDispatcher is not null && _viewDispatcher != dispatcher)
            {
                rejectionException = new InvalidOperationException(
                    "ホームHUDのViewは生成元のDispatcherから取得してください。");
            }
            else if (_views.TryGetValue(variant, out FrameworkElement? cached))
            {
                selected = cached;
            }
            else
            {
                _viewDispatcher = dispatcher;
                _views.Add(variant, created);
                selected = created;
            }
        }

        if (!ReferenceEquals(selected, created))
        {
            (created as IDisposable)?.Dispose();
        }

        if (rejectionException is not null)
        {
            ExceptionDispatchInfo.Capture(rejectionException).Throw();
        }

        selected!.DataContext = _source.ViewDataContext;
        return selected;
    }

    public HudSize GetPreferredSize(HudViewContext context)
    {
        lock (_syncRoot)
        {
            ThrowIfUnavailable();
            return HomeHudLayout.Calculate(
                ResolveVariant(_source.DesignVariant),
                _source.ActiveWidgets,
                _source.MaxWidgetWidth,
                _source.MaxWidgetHeight);
        }
    }

    public ValueTask DisposeAsync()
    {
        TaskCompletionSource<object?> completionSource;
        FrameworkElement[] views;
        Dispatcher? dispatcher;

        lock (_syncRoot)
        {
            if (_disposeTask is not null)
            {
                return new ValueTask(_disposeTask);
            }

            _isDisposing = true;
            views = _views.Values.ToArray();
            _views.Clear();
            dispatcher = _viewDispatcher;
            _viewDispatcher = null;
            completionSource = new TaskCompletionSource<object?>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _disposeTask = completionSource.Task;
        }

        _ = DisposeAndSignalCompletionAsync(views, dispatcher, completionSource);
        return new ValueTask(completionSource.Task);
    }

    private async Task DisposeAndSignalCompletionAsync(
        IReadOnlyList<FrameworkElement> views,
        Dispatcher? dispatcher,
        TaskCompletionSource<object?> completionSource)
    {
        try
        {
            await DisposeCoreAsync(views, dispatcher);
            completionSource.SetResult(null);
        }
        catch (Exception exception)
        {
            completionSource.SetException(exception);
        }
    }

    private async Task DisposeCoreAsync(
        IReadOnlyList<FrameworkElement> views,
        Dispatcher? dispatcher)
    {
        List<Exception> exceptions = [];
        await _lifecycleGate.WaitAsync();
        try
        {
            if (_isActive)
            {
                try
                {
                    _source.Stop();
                }
                catch (Exception exception)
                {
                    exceptions.Add(exception);
                }
                finally
                {
                    _isActive = false;
                }
            }

            if (_isInitialized)
            {
                try
                {
                    _source.PresentationInvalidated -= Source_PresentationInvalidated;
                }
                catch (Exception exception)
                {
                    exceptions.Add(exception);
                }
                finally
                {
                    _isInitialized = false;
                }
            }

            await DisposeViewsAsync(views, dispatcher, exceptions);

            try
            {
                _source.Dispose();
            }
            catch (Exception exception)
            {
                exceptions.Add(exception);
            }
        }
        finally
        {
            lock (_syncRoot)
            {
                _isDisposed = true;
            }

            _lifecycleGate.Release();
        }

        if (exceptions.Count == 1)
        {
            ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
        }

        if (exceptions.Count > 1)
        {
            throw new AggregateException(
                "Multiple Home HUD cleanup operations failed.",
                exceptions);
        }
    }

    private static async Task DisposeViewsAsync(
        IReadOnlyList<FrameworkElement> views,
        Dispatcher? dispatcher,
        ICollection<Exception> exceptions)
    {
        if (views.Count == 0 || dispatcher is null)
        {
            return;
        }

        void DisposeViews()
        {
            foreach (IDisposable view in views.OfType<IDisposable>())
            {
                try
                {
                    view.Dispose();
                }
                catch (Exception exception)
                {
                    exceptions.Add(exception);
                }
            }
        }

        if (dispatcher.CheckAccess())
        {
            DisposeViews();
            return;
        }

        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return;
        }

        try
        {
            await dispatcher.InvokeAsync(DisposeViews, DispatcherPriority.Send).Task;
        }
        catch (Exception exception)
        {
            exceptions.Add(exception);
        }
    }

    private bool IsUnavailable()
    {
        lock (_syncRoot)
        {
            return _isDisposing || _isDisposed;
        }
    }

    private void ThrowIfUnavailable() =>
        ObjectDisposedException.ThrowIf(_isDisposing || _isDisposed, this);

    private void EnsureViewDispatcher(Dispatcher dispatcher)
    {
        if (_viewDispatcher is not null && _viewDispatcher != dispatcher)
        {
            throw new InvalidOperationException(
                "ホームHUDのViewは生成元のDispatcherから取得してください。");
        }
    }

    private void Source_PresentationInvalidated(object? sender, EventArgs e)
    {
        lock (_syncRoot)
        {
            if (_isDisposing || _isDisposed)
            {
                return;
            }
        }

        PresentationInvalidated?.Invoke(this, EventArgs.Empty);
    }

    private static HomeHudDesignVariant ResolveVariant(HomeHudDesignVariant variant) =>
        Enum.IsDefined(variant) ? variant : HomeHudDesignVariant.FusionBalanced;
}
