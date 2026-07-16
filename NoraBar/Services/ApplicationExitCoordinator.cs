namespace NoraBar.Services;

internal enum ApplicationExitReason
{
    NormalShutdown,
    StartupFailure,
}

internal sealed record ApplicationExitResult(
    ApplicationExitReason Reason,
    int ExitCode,
    Exception? StartupException,
    IReadOnlyList<Exception> CleanupExceptions)
{
    internal StartupFailureReport CreateStartupFailureReport()
    {
        if (StartupException is null)
        {
            throw new InvalidOperationException(
                "A startup failure report requires a startup exception.");
        }

        return StartupFailureReport.Create(StartupException, CleanupExceptions);
    }
}

internal sealed class ApplicationExitCoordinator
{
    internal const int NormalExitCode = 0;
    internal const int StartupFailureExitCode = -1;

    private readonly object _syncRoot = new();
    private readonly ShutdownTaskCoordinator _shutdownCoordinator = new();
    private readonly ApplicationCleanupCoordinator _cleanupCoordinator = new();
    private readonly Func<Task<IReadOnlyList<Exception>>> _cleanup;
    private readonly Func<ApplicationExitResult, Task> _beforeShutdown;
    private readonly Action<int> _shutdown;
    private readonly TaskCompletionSource<ApplicationExitResult> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Exception? _startupException;
    private bool _startupFailureRegistrationClosed;

    internal ApplicationExitCoordinator(
        Func<Task<IReadOnlyList<Exception>>> cleanup,
        Func<ApplicationExitResult, Task> beforeShutdown,
        Action<int> shutdown)
    {
        ArgumentNullException.ThrowIfNull(cleanup);
        ArgumentNullException.ThrowIfNull(beforeShutdown);
        ArgumentNullException.ThrowIfNull(shutdown);

        _cleanup = cleanup;
        _beforeShutdown = beforeShutdown;
        _shutdown = shutdown;
    }

    internal IDisposable? TryBeginStartupCompletion()
    {
        IDisposable? startupCompletion =
            _shutdownCoordinator.TryBeginStartupCompletion();
        return startupCompletion is null
            ? null
            : new StartupCompletionLease(this, startupCompletion);
    }

    internal Task<ApplicationExitResult> RequestShutdownAsync()
    {
        _shutdownCoordinator.RunOnce(CompleteAsync);
        return _completion.Task;
    }

    internal Task<ApplicationExitResult> RequestStartupFailureAsync(
        Exception startupException)
    {
        ArgumentNullException.ThrowIfNull(startupException);

        lock (_syncRoot)
        {
            if (_startupFailureRegistrationClosed)
            {
                throw new InvalidOperationException(
                    "Startup failure registration is already closed.");
            }

            _startupException ??= startupException;
        }

        return RequestShutdownAsync();
    }

    private async Task CompleteAsync()
    {
        try
        {
            IReadOnlyList<Exception> cleanupExceptions =
                await _cleanupCoordinator.RunOnce(_cleanup);
            Exception? startupException;

            lock (_syncRoot)
            {
                // The startup lease closes failure registration before cleanup can run.
                startupException = _startupException;
            }

            ApplicationExitReason reason = startupException is null
                ? ApplicationExitReason.NormalShutdown
                : ApplicationExitReason.StartupFailure;
            int exitCode = reason == ApplicationExitReason.StartupFailure
                ? StartupFailureExitCode
                : NormalExitCode;
            var result = new ApplicationExitResult(
                reason,
                exitCode,
                startupException,
                cleanupExceptions);

            try
            {
                Task beforeShutdownTask = _beforeShutdown(result)
                    ?? throw new InvalidOperationException(
                        "The pre-shutdown handler must return a Task.");
                await beforeShutdownTask;
            }
            finally
            {
                _shutdown(exitCode);
            }

            _completion.TrySetResult(result);
        }
        catch (OperationCanceledException exception)
        {
            _completion.TrySetCanceled(exception.CancellationToken);
        }
        catch (Exception exception)
        {
            _completion.TrySetException(exception);
        }
    }

    private void CompleteStartup(IDisposable startupCompletion)
    {
        lock (_syncRoot)
        {
            _startupFailureRegistrationClosed = true;
        }

        startupCompletion.Dispose();
    }

    private sealed class StartupCompletionLease : IDisposable
    {
        private ApplicationExitCoordinator? _owner;
        private IDisposable? _startupCompletion;

        internal StartupCompletionLease(
            ApplicationExitCoordinator owner,
            IDisposable startupCompletion)
        {
            _owner = owner;
            _startupCompletion = startupCompletion;
        }

        public void Dispose()
        {
            ApplicationExitCoordinator? owner =
                Interlocked.Exchange(ref _owner, null);
            IDisposable? startupCompletion =
                Interlocked.Exchange(ref _startupCompletion, null);
            if (owner is not null && startupCompletion is not null)
            {
                owner.CompleteStartup(startupCompletion);
            }
        }
    }
}
