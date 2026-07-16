using NoraBar.Services;
using Xunit;

namespace NoraBar.Tests.Services;

public sealed class ApplicationExitCoordinatorTests
{
    [Fact]
    public async Task RunOnce_ConcurrentCallersExecuteCleanupOnce()
    {
        var cleanupGate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var expectedFailures = new Exception[]
        {
            new InvalidOperationException("cleanup failed"),
        };
        var coordinator = new ApplicationCleanupCoordinator();
        int cleanupCalls = 0;

        Task<IReadOnlyList<Exception>> first = coordinator.RunOnce(async () =>
        {
            Interlocked.Increment(ref cleanupCalls);
            await cleanupGate.Task;
            return expectedFailures;
        });
        Task<IReadOnlyList<Exception>> second = coordinator.RunOnce(
            () => throw new InvalidOperationException("must not run"));

        Assert.Same(first, second);
        Assert.Equal(1, Volatile.Read(ref cleanupCalls));

        cleanupGate.SetResult();
        IReadOnlyList<Exception> firstResult = await first;
        IReadOnlyList<Exception> secondResult = await second;

        Assert.Same(firstResult, secondResult);
        Assert.Equal(expectedFailures, firstResult);
    }

    [Fact]
    public async Task StartupFailureWhileShutdownWaitsForStartupCompletion_CleansUpOnce()
    {
        var startupFailure = new InvalidOperationException("startup failed");
        int cleanupCalls = 0;
        var shutdownCodes = new List<int>();
        var coordinator = CreateCoordinator(
            () =>
            {
                Interlocked.Increment(ref cleanupCalls);
                return Task.FromResult<IReadOnlyList<Exception>>([]);
            },
            shutdownCodes.Add);
        IDisposable startupCompletion = Assert.IsAssignableFrom<IDisposable>(
            coordinator.TryBeginStartupCompletion());

        Task<ApplicationExitResult> normalShutdown =
            coordinator.RequestShutdownAsync();
        Task<ApplicationExitResult> failedStartup =
            coordinator.RequestStartupFailureAsync(startupFailure);

        Assert.Same(normalShutdown, failedStartup);
        Assert.False(normalShutdown.IsCompleted);

        startupCompletion.Dispose();
        ApplicationExitResult result = await normalShutdown;

        Assert.Equal(1, cleanupCalls);
        Assert.Equal(ApplicationExitReason.StartupFailure, result.Reason);
        Assert.Equal(ApplicationExitCoordinator.StartupFailureExitCode, result.ExitCode);
        Assert.Same(startupFailure, result.StartupException);
        Assert.Equal(
            new[] { ApplicationExitCoordinator.StartupFailureExitCode },
            shutdownCodes);
    }

    [Fact]
    public async Task ConcurrentShutdownAndStartupFailure_PreserveCleanupFailures()
    {
        var startupFailure = new InvalidOperationException("startup failed");
        var routerFailure = new InvalidOperationException("router cleanup failed");
        var registryFailure = new InvalidOperationException("registry cleanup failed");
        var cleanupFailures = new Exception[] { routerFailure, registryFailure };
        StartupFailureReport? observedReport = null;
        var coordinator = new ApplicationExitCoordinator(
            () => Task.FromResult<IReadOnlyList<Exception>>(cleanupFailures),
            result =>
            {
                observedReport = result.CreateStartupFailureReport();
                return Task.CompletedTask;
            },
            _ => { });
        IDisposable startupCompletion = Assert.IsAssignableFrom<IDisposable>(
            coordinator.TryBeginStartupCompletion());

        Task<ApplicationExitResult> normalShutdown =
            coordinator.RequestShutdownAsync();
        Task<ApplicationExitResult> failedStartup =
            coordinator.RequestStartupFailureAsync(startupFailure);

        startupCompletion.Dispose();
        ApplicationExitResult normalResult = await normalShutdown;
        ApplicationExitResult startupResult = await failedStartup;

        Assert.Same(normalResult, startupResult);
        Assert.Equal(cleanupFailures, normalResult.CleanupExceptions);
        Assert.NotNull(observedReport);
        Assert.Same(startupFailure, observedReport.StartupException);
        Assert.Equal(cleanupFailures, observedReport.CleanupExceptions);
    }

    [Fact]
    public async Task ConcurrentRequests_InvokeApplicationShutdownOnce()
    {
        var startupFailure = new InvalidOperationException("startup failed");
        var shutdownCodes = new List<int>();
        using var startGate = new Barrier(3);
        using var requestsRegistered = new CountdownEvent(2);
        var coordinator = CreateCoordinator(
            () => Task.FromResult<IReadOnlyList<Exception>>([]),
            shutdownCodes.Add);
        IDisposable startupCompletion = Assert.IsAssignableFrom<IDisposable>(
            coordinator.TryBeginStartupCompletion());

        Task<ApplicationExitResult> normalShutdown = Task.Run(() =>
        {
            startGate.SignalAndWait();
            Task<ApplicationExitResult> request =
                coordinator.RequestShutdownAsync();
            requestsRegistered.Signal();
            return request;
        });
        Task<ApplicationExitResult> failedStartup = Task.Run(() =>
        {
            startGate.SignalAndWait();
            Task<ApplicationExitResult> request =
                coordinator.RequestStartupFailureAsync(startupFailure);
            requestsRegistered.Signal();
            return request;
        });

        startGate.SignalAndWait();
        Assert.True(requestsRegistered.Wait(TimeSpan.FromSeconds(5)));
        startupCompletion.Dispose();
        await Task.WhenAll(normalShutdown, failedStartup);

        Assert.Single(shutdownCodes);
        Assert.Equal(
            ApplicationExitCoordinator.StartupFailureExitCode,
            shutdownCodes[0]);
    }

    [Fact]
    public async Task RequestStartupFailureAsync_AfterStartupCompletionIsRejected()
    {
        var cleanupGate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var shutdownCodes = new List<int>();
        var coordinator = CreateCoordinator(
            async () =>
            {
                await cleanupGate.Task;
                return [];
            },
            shutdownCodes.Add);
        IDisposable startupCompletion = Assert.IsAssignableFrom<IDisposable>(
            coordinator.TryBeginStartupCompletion());
        Task<ApplicationExitResult> normalShutdown =
            coordinator.RequestShutdownAsync();

        startupCompletion.Dispose();

        Action lateRegistration = () =>
        {
            _ = coordinator.RequestStartupFailureAsync(
                new InvalidOperationException("too late"));
        };
        Assert.Throws<InvalidOperationException>(lateRegistration);

        cleanupGate.SetResult();
        ApplicationExitResult result = await normalShutdown;

        Assert.Equal(ApplicationExitReason.NormalShutdown, result.Reason);
        Assert.Equal(ApplicationExitCoordinator.NormalExitCode, result.ExitCode);
        Assert.Equal(
            new[] { ApplicationExitCoordinator.NormalExitCode },
            shutdownCodes);
    }

    [Fact]
    public async Task CleanupThrowsSynchronously_StillInvokesShutdownAndReturnsFailure()
    {
        var cleanupFailure = new InvalidOperationException("cleanup failed");
        var shutdownCodes = new List<int>();
        int beforeShutdownCalls = 0;
        var coordinator = CreateCoordinator(
            () => throw cleanupFailure,
            shutdownCodes.Add,
            _ => beforeShutdownCalls++);

        Task<ApplicationExitResult> exit = coordinator.RequestShutdownAsync();
        ApplicationExitResult result = await exit;

        Assert.False(exit.IsCanceled);
        Assert.Equal(new Exception[] { cleanupFailure }, result.CleanupExceptions);
        Assert.Equal(ApplicationExitReason.NormalShutdown, result.Reason);
        Assert.Equal(ApplicationExitCoordinator.NormalExitCode, result.ExitCode);
        Assert.Equal(1, beforeShutdownCalls);
        Assert.Equal(
            new[] { ApplicationExitCoordinator.NormalExitCode },
            shutdownCodes);
    }

    [Fact]
    public async Task CleanupReturnsFaultedTask_StillInvokesShutdownAndReturnsFailure()
    {
        var cleanupFailure = new InvalidOperationException("cleanup failed");
        var shutdownCodes = new List<int>();
        int beforeShutdownCalls = 0;
        var coordinator = CreateCoordinator(
            () => Task.FromException<IReadOnlyList<Exception>>(cleanupFailure),
            shutdownCodes.Add,
            _ => beforeShutdownCalls++);

        Task<ApplicationExitResult> exit = coordinator.RequestShutdownAsync();
        ApplicationExitResult result = await exit;

        Assert.False(exit.IsCanceled);
        Assert.Equal(new Exception[] { cleanupFailure }, result.CleanupExceptions);
        Assert.Equal(ApplicationExitReason.NormalShutdown, result.Reason);
        Assert.Equal(ApplicationExitCoordinator.NormalExitCode, result.ExitCode);
        Assert.Equal(1, beforeShutdownCalls);
        Assert.Equal(
            new[] { ApplicationExitCoordinator.NormalExitCode },
            shutdownCodes);
    }

    [Fact]
    public async Task CleanupIsCanceled_StillInvokesShutdownAndReturnsCancellationFailure()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var shutdownCodes = new List<int>();
        int beforeShutdownCalls = 0;
        var coordinator = CreateCoordinator(
            () => Task.FromCanceled<IReadOnlyList<Exception>>(
                cancellationSource.Token),
            shutdownCodes.Add,
            _ => beforeShutdownCalls++);

        Task<ApplicationExitResult> exit = coordinator.RequestShutdownAsync();
        ApplicationExitResult result = await exit;

        Assert.False(exit.IsCanceled);
        OperationCanceledException cancellationFailure =
            Assert.IsAssignableFrom<OperationCanceledException>(
                Assert.Single(result.CleanupExceptions));
        Assert.Equal(cancellationSource.Token, cancellationFailure.CancellationToken);
        Assert.Equal(ApplicationExitReason.NormalShutdown, result.Reason);
        Assert.Equal(ApplicationExitCoordinator.NormalExitCode, result.ExitCode);
        Assert.Equal(1, beforeShutdownCalls);
        Assert.Equal(
            new[] { ApplicationExitCoordinator.NormalExitCode },
            shutdownCodes);
    }

    [Fact]
    public async Task StartupFailureAndCleanupFault_PreserveBothAndUseFailureExitCode()
    {
        var startupFailure = new InvalidOperationException("startup failed");
        var cleanupFailure = new InvalidOperationException("cleanup failed");
        var shutdownCodes = new List<int>();
        StartupFailureReport? failureReport = null;
        var coordinator = CreateCoordinator(
            () => Task.FromException<IReadOnlyList<Exception>>(cleanupFailure),
            shutdownCodes.Add,
            result => failureReport = result.CreateStartupFailureReport());
        IDisposable startupCompletion = Assert.IsAssignableFrom<IDisposable>(
            coordinator.TryBeginStartupCompletion());

        Task<ApplicationExitResult> exit =
            coordinator.RequestStartupFailureAsync(startupFailure);
        startupCompletion.Dispose();
        ApplicationExitResult result = await exit;

        Assert.False(exit.IsCanceled);
        Assert.Same(startupFailure, result.StartupException);
        Assert.Equal(new Exception[] { cleanupFailure }, result.CleanupExceptions);
        Assert.NotNull(failureReport);
        Assert.Same(startupFailure, failureReport.StartupException);
        Assert.Equal(
            new Exception[] { cleanupFailure },
            failureReport.CleanupExceptions);
        Assert.Equal(ApplicationExitReason.StartupFailure, result.Reason);
        Assert.Equal(ApplicationExitCoordinator.StartupFailureExitCode, result.ExitCode);
        Assert.Equal(
            new[] { ApplicationExitCoordinator.StartupFailureExitCode },
            shutdownCodes);
    }

    [Fact]
    public async Task CleanupReturnsNullResult_StillShutsDownWithDiagnosticFailure()
    {
        var shutdownCodes = new List<int>();
        int beforeShutdownCalls = 0;
        var coordinator = CreateCoordinator(
            () => Task.FromResult<IReadOnlyList<Exception>>(null!),
            shutdownCodes.Add,
            _ => beforeShutdownCalls++);

        Task<ApplicationExitResult> exit = coordinator.RequestShutdownAsync();
        ApplicationExitResult result = await exit;

        Assert.False(exit.IsCanceled);
        InvalidOperationException diagnosticFailure =
            Assert.IsType<InvalidOperationException>(
                Assert.Single(result.CleanupExceptions));
        Assert.Contains(
            "result",
            diagnosticFailure.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ApplicationExitReason.NormalShutdown, result.Reason);
        Assert.Equal(ApplicationExitCoordinator.NormalExitCode, result.ExitCode);
        Assert.Equal(1, beforeShutdownCalls);
        Assert.Equal(
            new[] { ApplicationExitCoordinator.NormalExitCode },
            shutdownCodes);
    }

    private static ApplicationExitCoordinator CreateCoordinator(
        Func<Task<IReadOnlyList<Exception>>> cleanup,
        Action<int> shutdown,
        Action<ApplicationExitResult>? beforeShutdown = null)
    {
        return new ApplicationExitCoordinator(
            cleanup,
            result =>
            {
                beforeShutdown?.Invoke(result);
                return Task.CompletedTask;
            },
            shutdown);
    }
}
