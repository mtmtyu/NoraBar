using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;
using NoraBar.Models;
using NoraBar.ViewModels;

namespace NoraBar.Hud.Home;

internal sealed class HomeHudModule : IHudModule
{
    private static readonly TimeSpan DefaultViewDisposalTimeout =
        TimeSpan.FromSeconds(5);

    private readonly object _syncRoot = new();
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly IHomeHudPresentationSource _source;
    private readonly Func<HomeHudDesignVariant, FrameworkElement> _createView;
    private readonly TimeSpan _viewDisposalTimeout;
    private readonly Action<Exception> _reportLateCleanupFailure;
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
        Func<HomeHudDesignVariant, FrameworkElement> createView,
        TimeSpan? viewDisposalTimeout = null,
        Action<Exception>? reportLateCleanupFailure = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(createView);
        _source = source;
        _createView = createView;
        _viewDisposalTimeout = viewDisposalTimeout ?? DefaultViewDisposalTimeout;
        _reportLateCleanupFailure = reportLateCleanupFailure
            ?? (exception => Trace.TraceError(
                $"Late Home HUD cleanup failed: {exception}"));
        if (_viewDisposalTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(viewDisposalTimeout));
        }
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
            ThrowAfterRejectedViewCleanup(
                created,
                new InvalidOperationException(
                    "ホームHUDのViewは現在のDispatcherで生成する必要があります。"));
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

        if (rejectionException is not null)
        {
            ThrowAfterRejectedViewCleanup(created, rejectionException);
        }

        if (!ReferenceEquals(selected, created))
        {
            ThrowIfCleanupFailed(CleanupViews([created]));
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

            ViewDisposalOutcome viewDisposal = await DisposeViewsAsync(
                views,
                dispatcher,
                _viewDisposalTimeout);
            exceptions.AddRange(viewDisposal.Exceptions);

            if (viewDisposal.LateCompletion is not null)
            {
                _ = CompleteDeferredCleanupAsync(viewDisposal.LateCompletion);
            }
            else
            {
                DisposeSource(exceptions);
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

    private async Task CompleteDeferredCleanupAsync(
        Task<IReadOnlyList<Exception>> lateViewCleanup)
    {
        List<Exception> exceptions = [.. await lateViewCleanup];
        DisposeSource(exceptions);
        if (exceptions.Count == 0)
        {
            return;
        }

        var failure = new AggregateException(
            "Late Home HUD cleanup operations failed.",
            exceptions);
        try
        {
            _reportLateCleanupFailure(failure);
        }
        catch (Exception reportingException)
        {
            Trace.TraceError(
                $"Late Home HUD cleanup failure reporting failed: {reportingException}");
        }
    }

    private void DisposeSource(ICollection<Exception> exceptions)
    {
        try
        {
            _source.Dispose();
        }
        catch (Exception exception)
        {
            exceptions.Add(exception);
        }
    }

    private static async Task<ViewDisposalOutcome> DisposeViewsAsync(
        IReadOnlyList<FrameworkElement> views,
        Dispatcher? dispatcher,
        TimeSpan timeout)
    {
        if (views.Count == 0 || dispatcher is null)
        {
            return ViewDisposalOutcome.Completed([]);
        }

        if (!views.Any(view => view is IDisposable or IHomeHudManagedResource))
        {
            return ViewDisposalOutcome.Completed([]);
        }

        if (dispatcher.CheckAccess())
        {
            return ViewDisposalOutcome.Completed(CleanupViews(views));
        }

        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            List<Exception> exceptions = [.. ReleaseManagedResources(views)];
            exceptions.Add(new InvalidOperationException(
                "Home HUD views could not be disposed because their Dispatcher is shutting down."));
            return ViewDisposalOutcome.Completed(exceptions);
        }

        var callbackCompletion = new TaskCompletionSource<IReadOnlyList<Exception>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        DispatcherOperation operation = dispatcher.InvokeAsync(
            () => callbackCompletion.TrySetResult(CleanupViews(views)),
            DispatcherPriority.Send);
        try
        {
            await operation.Task.WaitAsync(timeout);
            return ViewDisposalOutcome.Completed(await callbackCompletion.Task);
        }
        catch (TimeoutException)
        {
            var timeoutException = new TimeoutException(
                $"Home HUD view disposal did not complete within {timeout}.");
            if (operation.Abort())
            {
                await ObserveAbortedOperationAsync(operation.Task);
                List<Exception> exceptions = [.. ReleaseManagedResources(views)];
                exceptions.Add(timeoutException);
                return ViewDisposalOutcome.Completed(exceptions);
            }

            return ViewDisposalOutcome.Executing(
                timeoutException,
                ObserveLateOperationAsync(operation.Task, callbackCompletion.Task));
        }
        catch (Exception exception)
        {
            List<Exception> exceptions = [exception, .. ReleaseManagedResources(views)];
            return ViewDisposalOutcome.Completed(exceptions);
        }
    }

    private void ThrowAfterRejectedViewCleanup(
        FrameworkElement view,
        Exception rejectionException)
    {
        ViewDisposalOutcome outcome = DisposeViewOnOwningDispatcher(
            view,
            _viewDisposalTimeout);
        IReadOnlyList<Exception> cleanupExceptions = outcome.Exceptions;
        if (outcome.LateCompletion is not null)
        {
            _ = ReportLateRejectedViewCleanupAsync(outcome.LateCompletion);
        }

        if (cleanupExceptions.Count > 0)
        {
            throw new AggregateException(
                "Home HUD view rejection and cleanup failed.",
                [rejectionException, .. cleanupExceptions]);
        }

        ExceptionDispatchInfo.Capture(rejectionException).Throw();
    }

    private async Task ReportLateRejectedViewCleanupAsync(
        Task<IReadOnlyList<Exception>> lateCompletion)
    {
        IReadOnlyList<Exception> exceptions = await lateCompletion;
        if (exceptions.Count == 0)
        {
            return;
        }

        try
        {
            _reportLateCleanupFailure(new AggregateException(
                "Late rejected Home HUD view cleanup failed.",
                exceptions));
        }
        catch (Exception reportingException)
        {
            Trace.TraceError(
                $"Late rejected Home HUD cleanup failure reporting failed: {reportingException}");
        }
    }

    private static ViewDisposalOutcome DisposeViewOnOwningDispatcher(
        FrameworkElement view,
        TimeSpan timeout)
    {
        if (view is not IDisposable && view is not IHomeHudManagedResource)
        {
            return ViewDisposalOutcome.Completed([]);
        }

        Dispatcher dispatcher = view.Dispatcher;
        if (dispatcher.CheckAccess())
        {
            return ViewDisposalOutcome.Completed(CleanupViews([view]));
        }

        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            List<Exception> exceptions = [.. ReleaseManagedResources([view])];
            exceptions.Add(new InvalidOperationException(
                "A rejected Home HUD view could not be disposed because its Dispatcher is shutting down."));
            return ViewDisposalOutcome.Completed(exceptions);
        }

        var callbackCompletion = new TaskCompletionSource<IReadOnlyList<Exception>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        DispatcherOperation operation = dispatcher.InvokeAsync(
            () => callbackCompletion.TrySetResult(CleanupViews([view])),
            DispatcherPriority.Send);
        try
        {
            operation.Task.WaitAsync(timeout).GetAwaiter().GetResult();
            return ViewDisposalOutcome.Completed(
                callbackCompletion.Task.GetAwaiter().GetResult());
        }
        catch (TimeoutException)
        {
            var timeoutException = new TimeoutException(
                $"Rejected Home HUD view disposal did not complete within {timeout}.");
            if (operation.Abort())
            {
                ObserveAbortedOperationAsync(operation.Task).GetAwaiter().GetResult();
                List<Exception> exceptions = [.. ReleaseManagedResources([view])];
                exceptions.Add(timeoutException);
                return ViewDisposalOutcome.Completed(exceptions);
            }

            return ViewDisposalOutcome.Executing(
                timeoutException,
                ObserveLateOperationAsync(operation.Task, callbackCompletion.Task));
        }
        catch (Exception exception)
        {
            return ViewDisposalOutcome.Completed(
                [exception, .. ReleaseManagedResources([view])]);
        }
    }

    private static IReadOnlyList<Exception> CleanupViews(
        IEnumerable<FrameworkElement> views)
    {
        List<Exception> exceptions = [];
        foreach (FrameworkElement view in views)
        {
            bool disposeSucceeded = false;
            if (view is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                    disposeSucceeded = true;
                }
                catch (Exception exception)
                {
                    exceptions.Add(exception);
                }
            }

            if (!disposeSucceeded && view is IHomeHudManagedResource managedResource)
            {
                TryReleaseManagedResource(managedResource, exceptions);
            }
        }

        return exceptions;
    }

    private static IReadOnlyList<Exception> ReleaseManagedResources(
        IEnumerable<FrameworkElement> views)
    {
        List<Exception> exceptions = [];
        foreach (IHomeHudManagedResource view in views.OfType<IHomeHudManagedResource>())
        {
            TryReleaseManagedResource(view, exceptions);
        }

        return exceptions;
    }

    private static void TryReleaseManagedResource(
        IHomeHudManagedResource resource,
        ICollection<Exception> exceptions)
    {
        try
        {
            resource.ReleaseManagedResources();
        }
        catch (Exception exception)
        {
            exceptions.Add(exception);
        }
    }

    private static async Task ObserveAbortedOperationAsync(Task operationTask)
    {
        try
        {
            await operationTask;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static async Task<IReadOnlyList<Exception>> ObserveLateOperationAsync(
        Task operationTask,
        Task<IReadOnlyList<Exception>> callbackCompletion)
    {
        try
        {
            await operationTask;
            return await callbackCompletion;
        }
        catch (Exception exception)
        {
            return [exception];
        }
    }

    private static void ThrowIfCleanupFailed(IReadOnlyList<Exception> exceptions)
    {
        if (exceptions.Count == 1)
        {
            ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
        }

        if (exceptions.Count > 1)
        {
            throw new AggregateException(
                "Multiple Home HUD view cleanup operations failed.",
                exceptions);
        }
    }

    private sealed record ViewDisposalOutcome(
        IReadOnlyList<Exception> Exceptions,
        Task<IReadOnlyList<Exception>>? LateCompletion)
    {
        internal static ViewDisposalOutcome Completed(
            IReadOnlyList<Exception> exceptions) =>
            new(exceptions, null);

        internal static ViewDisposalOutcome Executing(
            Exception timeoutException,
            Task<IReadOnlyList<Exception>> lateCompletion) =>
            new([timeoutException], lateCompletion);
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
