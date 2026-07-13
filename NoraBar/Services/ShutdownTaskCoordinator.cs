namespace NoraBar.Services;

internal sealed class ShutdownTaskCoordinator
{
    private readonly object _syncRoot = new();
    private Task? _shutdownTask;
    private TaskCompletionSource? _startupCompletion;

    internal bool IsStarted
    {
        get
        {
            lock (_syncRoot)
            {
                return _shutdownTask is not null;
            }
        }
    }

    internal Task RunOnce(Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        TaskCompletionSource shutdownCompletion;
        Task startupCompletion;

        lock (_syncRoot)
        {
            if (_shutdownTask is not null)
            {
                return _shutdownTask;
            }

            shutdownCompletion = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _shutdownTask = shutdownCompletion.Task;
            startupCompletion = _startupCompletion?.Task ?? Task.CompletedTask;
        }

        _ = CompleteAsync(startupCompletion, operation, shutdownCompletion);
        return shutdownCompletion.Task;
    }

    internal IDisposable? TryBeginStartupCompletion()
    {
        lock (_syncRoot)
        {
            if (_shutdownTask is not null)
            {
                return null;
            }

            if (_startupCompletion is not null)
            {
                throw new InvalidOperationException("起動完了処理は既に開始されています。");
            }

            _startupCompletion = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            return new StartupCompletionLease(this, _startupCompletion);
        }
    }

    private static async Task CompleteAsync(
        Task startupCompletion,
        Func<Task> operation,
        TaskCompletionSource shutdownCompletion)
    {
        try
        {
            await startupCompletion;
            Task operationTask = operation()
                ?? throw new InvalidOperationException(
                    "終了処理はTaskを返す必要があります。");
            await operationTask.ConfigureAwait(false);
            shutdownCompletion.TrySetResult();
        }
        catch (OperationCanceledException exception)
        {
            shutdownCompletion.TrySetCanceled(exception.CancellationToken);
        }
        catch (Exception exception)
        {
            shutdownCompletion.TrySetException(exception);
        }
    }

    private void CompleteStartup(TaskCompletionSource startupCompletion)
    {
        lock (_syncRoot)
        {
            if (ReferenceEquals(_startupCompletion, startupCompletion))
            {
                _startupCompletion = null;
            }
        }

        startupCompletion.TrySetResult();
    }

    private sealed class StartupCompletionLease : IDisposable
    {
        private ShutdownTaskCoordinator? _owner;
        private readonly TaskCompletionSource _completion;

        internal StartupCompletionLease(
            ShutdownTaskCoordinator owner,
            TaskCompletionSource completion)
        {
            _owner = owner;
            _completion = completion;
        }

        public void Dispose()
        {
            ShutdownTaskCoordinator? owner = Interlocked.Exchange(ref _owner, null);
            owner?.CompleteStartup(_completion);
        }
    }
}
