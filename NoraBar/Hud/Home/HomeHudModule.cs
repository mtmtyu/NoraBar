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
    private bool _isInitialized;
    private bool _isActive;
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
            if (_isInitialized || _isDisposed)
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
            if (_isDisposed || _isActive)
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
            if (_isDisposed || !_isActive)
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
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            if (_viewDispatcher is not null && _viewDispatcher != dispatcher)
            {
                throw new InvalidOperationException(
                    "ホームHUDのViewは生成元のDispatcherから取得してください。");
            }

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

        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            if (!_views.TryGetValue(variant, out FrameworkElement? view))
            {
                _viewDispatcher = dispatcher;
                view = created;
                _views.Add(variant, view);
            }

            view.DataContext = _source.ViewDataContext;
            return view;
        }
    }

    public HudSize GetPreferredSize(HudViewContext context)
    {
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            return HomeHudLayout.Calculate(
                ResolveVariant(_source.DesignVariant),
                _source.ActiveWidgets,
                _source.MaxWidgetWidth,
                _source.MaxWidgetHeight);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _lifecycleGate.WaitAsync();
        try
        {
            if (_isDisposed)
            {
                return;
            }

            if (_isActive)
            {
                _source.Stop();
                _isActive = false;
            }

            if (_isInitialized)
            {
                _source.PresentationInvalidated -= Source_PresentationInvalidated;
                _isInitialized = false;
            }

            _source.Dispose();
            _isDisposed = true;
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private void Source_PresentationInvalidated(object? sender, EventArgs e) =>
        PresentationInvalidated?.Invoke(this, EventArgs.Empty);

    private static HomeHudDesignVariant ResolveVariant(HomeHudDesignVariant variant) =>
        Enum.IsDefined(variant) ? variant : HomeHudDesignVariant.FusionBalanced;
}
