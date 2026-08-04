using System.Windows;

namespace NoraBar.Hud.Launcher;

internal sealed class LauncherHudModule : IHudModule
{
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly ILauncherHudPresentationSource _source;
    private readonly Func<FrameworkElement> _createView;
    private FrameworkElement? _view;
    private bool _isInitialized;
    private bool _isActive;
    private bool _isDisposed;

    internal LauncherHudModule(ILauncherHudPresentationSource source, Func<FrameworkElement> createView)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(createView);
        _source = source;
        _createView = createView;
    }

    public string Id => BuiltInHudIds.Launcher;

    public HudModuleMetadata Metadata { get; } = new("Launcher", 2);

    public event EventHandler? PresentationInvalidated;

    internal FrameworkElement? CachedView => _view;

    public async ValueTask InitializeAsync(CancellationToken cancellationToken)
    {
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized || _isDisposed) return;
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
            if (_isActive || _isDisposed) return;
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
            if (!_isActive || _isDisposed) return;
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
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (_source is ILauncherHudStateSink stateSink)
        {
            stateSink.SetPresentationState(context.PresentationState);
        }
        _view ??= _createView();
        if (!ReferenceEquals(_view.DataContext, _source.ViewDataContext))
        {
            _view.DataContext = _source.ViewDataContext;
        }
        return _view;
    }

    public HudSize GetPreferredSize(HudViewContext context) =>
        LauncherHudLayout.Calculate(context.PresentationState);

    public async ValueTask DisposeAsync()
    {
        await _lifecycleGate.WaitAsync();
        try
        {
            if (_isDisposed) return;
            if (_isInitialized)
            {
                _source.PresentationInvalidated -= Source_PresentationInvalidated;
            }
            if (_isActive)
            {
                _source.Stop();
                _isActive = false;
            }
            _source.Dispose();
            (_view as IDisposable)?.Dispose();
            _isDisposed = true;
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private void Source_PresentationInvalidated(object? sender, EventArgs e)
    {
        if (!_isDisposed)
        {
            PresentationInvalidated?.Invoke(this, EventArgs.Empty);
        }
    }
}
