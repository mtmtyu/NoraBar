using System.Collections.Concurrent;
using NoraBar.Services;
using Xunit;

namespace NoraBar.Tests.Services;

public sealed class ShutdownTaskCoordinatorTests
{
    [Fact]
    public async Task RunOnce_ReturnsSameTaskAndExecutesOperationOnce()
    {
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new ShutdownTaskCoordinator();
        int calls = 0;

        Task first = coordinator.RunOnce(() =>
        {
            Interlocked.Increment(ref calls);
            return completion.Task;
        });
        Task second = coordinator.RunOnce(() => throw new InvalidOperationException());

        Assert.Same(first, second);
        Assert.Equal(1, calls);
        completion.SetResult();
        await first;
    }

    [Fact]
    public async Task RunOnce_SynchronousFailureIsStoredInSharedTask()
    {
        var coordinator = new ShutdownTaskCoordinator();
        var expected = new InvalidOperationException("failure");

        Task first = coordinator.RunOnce(() => throw expected);
        Task second = coordinator.RunOnce(() => Task.CompletedTask);

        Assert.Same(first, second);
        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await first);
        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task RunOnce_SynchronousReentrancyReturnsSharedTaskWithoutRestartingOperation()
    {
        var coordinator = new ShutdownTaskCoordinator();
        Task? reentrantTask = null;
        int calls = 0;

        Task first = coordinator.RunOnce(() =>
        {
            Interlocked.Increment(ref calls);
            reentrantTask = coordinator.RunOnce(() =>
            {
                Interlocked.Increment(ref calls);
                return Task.CompletedTask;
            });
            return Task.CompletedTask;
        });

        Assert.Same(first, reentrantTask);
        Assert.Equal(1, calls);
        await first;
    }

    [Fact]
    public async Task TryBeginStartupCompletion_ReturnsNullAfterShutdownStarts()
    {
        var coordinator = new ShutdownTaskCoordinator();

        Task shutdown = coordinator.RunOnce(() => Task.CompletedTask);
        IDisposable? startupCompletion = coordinator.TryBeginStartupCompletion();

        Assert.True(coordinator.IsStarted);
        Assert.Null(startupCompletion);
        await shutdown;
    }

    [Fact]
    public async Task RunOnce_WaitsForActiveStartupCompletionBeforeShutdownOperation()
    {
        var coordinator = new ShutdownTaskCoordinator();
        IDisposable startupCompletion = Assert.IsAssignableFrom<IDisposable>(
            coordinator.TryBeginStartupCompletion());
        bool operationStarted = false;

        Task shutdown = coordinator.RunOnce(() =>
        {
            operationStarted = true;
            return Task.CompletedTask;
        });

        Assert.False(operationStarted);
        Assert.False(shutdown.IsCompleted);

        startupCompletion.Dispose();

        await shutdown;
        Assert.True(operationStarted);
    }

    [Fact]
    public async Task RunOnce_AfterStartupCompletion_RunsOperationOnCallingSynchronizationContext()
    {
        var coordinator = new ShutdownTaskCoordinator();
        using var ready = new ManualResetEventSlim();
        using var context = new PumpSynchronizationContext();
        IDisposable? startupCompletion = null;
        Task? shutdown = null;
        int callingThreadId = 0;
        int operationThreadId = 0;
        SynchronizationContext? operationContext = null;

        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(context);
            callingThreadId = Environment.CurrentManagedThreadId;
            startupCompletion = coordinator.TryBeginStartupCompletion();
            shutdown = coordinator.RunOnce(() =>
            {
                operationThreadId = Environment.CurrentManagedThreadId;
                operationContext = SynchronizationContext.Current;
                context.Complete();
                return Task.CompletedTask;
            });
            ready.Set();
            context.RunOnCurrentThread();
        })
        {
            IsBackground = true
        };

        thread.Start();
        Assert.True(ready.Wait(TimeSpan.FromSeconds(5)));

        Assert.NotNull(startupCompletion);
        Assert.NotNull(shutdown);
        startupCompletion.Dispose();
        await shutdown.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(thread.Join(TimeSpan.FromSeconds(5)));

        Assert.Equal(callingThreadId, operationThreadId);
        Assert.Same(context, operationContext);
    }

    [Fact]
    public void RunOnce_NullOperationIsRejected()
    {
        var coordinator = new ShutdownTaskCoordinator();

        Action action = () => coordinator.RunOnce(null!);

        Assert.Throws<ArgumentNullException>(action);
    }

    private sealed class PumpSynchronizationContext : SynchronizationContext, IDisposable
    {
        private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)>
            _callbacks = new();

        public override void Post(SendOrPostCallback d, object? state)
        {
            _callbacks.Add((d, state));
        }

        internal void RunOnCurrentThread()
        {
            foreach ((SendOrPostCallback callback, object? state) in
                     _callbacks.GetConsumingEnumerable())
            {
                callback(state);
            }
        }

        internal void Complete()
        {
            _callbacks.CompleteAdding();
        }

        public void Dispose()
        {
            _callbacks.Dispose();
        }
    }
}
