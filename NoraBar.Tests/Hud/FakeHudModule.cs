using System.Windows;
using NoraBar.Hud;

namespace NoraBar.Tests.Hud;

internal sealed class FakeHudModule : IHudModule
{
    private readonly List<string>? _calls;
    private EventHandler? _presentationInvalidated;

    public FakeHudModule(string id, List<string>? calls = null)
    {
        Id = id;
        _calls = calls;
        Metadata = new HudModuleMetadata(id, 0);
    }

    public string Id { get; }

    public HudModuleMetadata Metadata { get; }

    public int InitializeCount { get; private set; }

    public int ActivateCount { get; private set; }

    public int DeactivateCount { get; private set; }

    public int DisposeCount { get; private set; }

    public Exception? ActivateException { get; set; }

    public Exception? DisposeException { get; set; }

    public bool InvalidateDuringActivate { get; set; }

    public TaskCompletionSource<bool>? DisposeStartedSignal { get; set; }

    public Task? DisposeWaitTask { get; set; }

    public event EventHandler? PresentationInvalidated
    {
        add
        {
            _calls?.Add($"{Id}:subscribe");
            _presentationInvalidated += value;
        }
        remove
        {
            _calls?.Add($"{Id}:unsubscribe");
            _presentationInvalidated -= value;
        }
    }

    public ValueTask InitializeAsync(CancellationToken cancellationToken)
    {
        InitializeCount++;
        _calls?.Add($"{Id}:initialize");
        return ValueTask.CompletedTask;
    }

    public ValueTask ActivateAsync(CancellationToken cancellationToken)
    {
        ActivateCount++;
        _calls?.Add($"{Id}:activate");

        if (ActivateException is not null)
        {
            return ValueTask.FromException(ActivateException);
        }

        if (InvalidateDuringActivate)
        {
            RaisePresentationInvalidated();
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask DeactivateAsync(CancellationToken cancellationToken)
    {
        DeactivateCount++;
        _calls?.Add($"{Id}:deactivate");
        return ValueTask.CompletedTask;
    }

    public FrameworkElement GetView(HudViewContext context)
    {
        throw new InvalidOperationException("FakeHudModule does not provide a view.");
    }

    public HudSize GetPreferredSize(HudViewContext context)
    {
        return new HudSize(1, 1);
    }

    public async ValueTask DisposeAsync()
    {
        DisposeCount++;
        _calls?.Add($"{Id}:dispose");
        DisposeStartedSignal?.TrySetResult(true);

        if (DisposeWaitTask is not null)
        {
            await DisposeWaitTask;
        }

        if (DisposeException is not null)
        {
            throw DisposeException;
        }
    }

    public void RaisePresentationInvalidated()
    {
        _calls?.Add($"{Id}:invalidate");
        _presentationInvalidated?.Invoke(this, EventArgs.Empty);
    }
}
