namespace NoraBar.Services;

internal sealed class ApplicationCleanupCoordinator
{
    private readonly object _syncRoot = new();
    private Task<IReadOnlyList<Exception>>? _cleanupTask;

    internal Task<IReadOnlyList<Exception>> RunOnce(
        Func<Task<IReadOnlyList<Exception>>> cleanup)
    {
        ArgumentNullException.ThrowIfNull(cleanup);

        TaskCompletionSource<IReadOnlyList<Exception>> completion;

        lock (_syncRoot)
        {
            if (_cleanupTask is not null)
            {
                return _cleanupTask;
            }

            completion = new TaskCompletionSource<IReadOnlyList<Exception>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _cleanupTask = completion.Task;
        }

        _ = CompleteAsync(cleanup, completion);
        return completion.Task;
    }

    private static async Task CompleteAsync(
        Func<Task<IReadOnlyList<Exception>>> cleanup,
        TaskCompletionSource<IReadOnlyList<Exception>> completion)
    {
        try
        {
            Task<IReadOnlyList<Exception>> cleanupTask = cleanup()
                ?? throw new InvalidOperationException(
                    "Cleanup must return a Task.");
            IReadOnlyList<Exception> cleanupExceptions = await cleanupTask;
            Exception[] capturedExceptions = cleanupExceptions.ToArray();
            completion.TrySetResult(Array.AsReadOnly(capturedExceptions));
        }
        catch (OperationCanceledException exception)
        {
            completion.TrySetCanceled(exception.CancellationToken);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }
}
