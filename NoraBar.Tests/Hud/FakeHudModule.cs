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

    public Exception? InitializeException { get; set; }

    public Exception? ActivateException { get; set; }

    public Queue<Exception?> ActivateResults { get; } = new();

    public Exception? DeactivateException { get; set; }

    public Exception? DisposeException { get; set; }

    public bool InvalidateDuringActivate { get; set; }

    public int InvalidationsDuringActivate { get; set; }

    public TaskCompletionSource<bool>? ActivateStartedSignal { get; set; }

    public Task? ActivateWaitTask { get; set; }

    public TaskCompletionSource<bool>? InitializeStartedSignal { get; set; }

    public Task? InitializeWaitTask { get; set; }

    public TaskCompletionSource<bool>? DeactivateStartedSignal { get; set; }

    public Task? DeactivateWaitTask { get; set; }

    public Exception? SubscribeException { get; set; }

    public Exception? SubscribeAfterAddException { get; set; }

    public Exception? UnsubscribeException { get; set; }

    public bool UnsubscribeExceptionOnce { get; set; }

    public TaskCompletionSource<bool>? DisposeStartedSignal { get; set; }

    public Task? DisposeWaitTask { get; set; }

    public event EventHandler? PresentationInvalidated
    {
        add
        {
            _calls?.Add($"{Id}:subscribe");
            if (SubscribeException is not null)
            {
                throw SubscribeException;
            }

            _presentationInvalidated += value;
            if (SubscribeAfterAddException is not null)
            {
                throw SubscribeAfterAddException;
            }
        }
        remove
        {
            _calls?.Add($"{Id}:unsubscribe");
            if (UnsubscribeException is not null)
            {
                Exception exception = UnsubscribeException;
                if (UnsubscribeExceptionOnce)
                {
                    UnsubscribeException = null;
                }

                throw exception;
            }

            _presentationInvalidated -= value;
        }
    }

    public async ValueTask InitializeAsync(CancellationToken cancellationToken)
    {
        InitializeCount++;
        _calls?.Add($"{Id}:initialize");
        InitializeStartedSignal?.TrySetResult(true);
        if (InitializeWaitTask is not null)
        {
            await InitializeWaitTask.WaitAsync(cancellationToken);
        }

        if (InitializeException is not null)
        {
            throw InitializeException;
        }
    }

    public async ValueTask ActivateAsync(CancellationToken cancellationToken)
    {
        ActivateCount++;
        _calls?.Add($"{Id}:activate");

        ActivateStartedSignal?.TrySetResult(true);
        if (ActivateWaitTask is not null)
        {
            await ActivateWaitTask;
        }

        Exception? activateException = ActivateResults.Count > 0
            ? ActivateResults.Dequeue()
            : ActivateException;
        if (activateException is not null)
        {
            throw activateException;
        }

        if (InvalidateDuringActivate)
        {
            RaisePresentationInvalidated();
        }

        for (int index = 0; index < InvalidationsDuringActivate; index++)
        {
            RaisePresentationInvalidated();
        }
    }

    public async ValueTask DeactivateAsync(CancellationToken cancellationToken)
    {
        DeactivateCount++;
        _calls?.Add($"{Id}:deactivate");

        DeactivateStartedSignal?.TrySetResult(true);
        if (DeactivateWaitTask is not null)
        {
            await DeactivateWaitTask.WaitAsync(cancellationToken);
        }

        if (DeactivateException is not null)
        {
            throw DeactivateException;
        }
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
