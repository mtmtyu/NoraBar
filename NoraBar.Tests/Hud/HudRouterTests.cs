using NoraBar.Hud;
using Xunit;

namespace NoraBar.Tests.Hud;

public sealed class HudRouterTests
{
    [Fact]
    public async Task InitializeAsync_SelectsMusicCollapsedAndActivatesOnce()
    {
        var music = new FakeHudModule(BuiltInHudIds.Music);
        var router = new HudRouter(CreateRegistry(music), BuiltInHudIds.Music, [BuiltInHudIds.Music]);
        await router.InitializeAsync(CancellationToken.None);
        await router.InitializeAsync(CancellationToken.None);
        Assert.Same(music, router.CurrentModule);
        Assert.Equal(BuiltInHudIds.Music, router.CurrentHudId);
        Assert.Equal(HudPresentationState.Collapsed, router.PresentationState);
        Assert.Equal(1, music.InitializeCount);
        Assert.Equal(1, music.ActivateCount);
    }

    [Fact]
    public async Task InitializeAsync_PublishesInvalidationRaisedDuringInitialActivation()
    {
        var music = new FakeHudModule(BuiltInHudIds.Music)
        {
            InvalidateDuringActivate = true
        };
        var router = new HudRouter(
            CreateRegistry(music),
            BuiltInHudIds.Music,
            [BuiltInHudIds.Music]);
        int presentationChangedCount = 0;
        router.PresentationChanged += (_, _) => presentationChangedCount++;

        await router.InitializeAsync(CancellationToken.None);

        Assert.Equal(1, presentationChangedCount);
    }

    [Fact]
    public async Task PresentationChanges_DoNotChangeModuleLifecycle()
    {
        var music = new FakeHudModule(BuiltInHudIds.Music);
        HudRouter router = await CreateInitializedRouterAsync(music);
        Assert.True(router.SetPresentationState(HudPresentationState.Expanded));
        Assert.True(router.SetPresentationState(HudPresentationState.Pinned));
        Assert.False(router.CollapseFromPointerLeave());
        Assert.Equal(HudPresentationState.Pinned, router.PresentationState);
        Assert.Equal(1, music.ActivateCount);
        Assert.Equal(0, music.DeactivateCount);
    }

    [Fact]
    public async Task GetSnapshot_ReturnsOneConsistentInitializedState()
    {
        var music = new FakeHudModule(BuiltInHudIds.Music);
        HudRouter router = await CreateInitializedRouterAsync(music);
        HudRouterSnapshot snapshot = router.GetSnapshot();
        Assert.True(snapshot.IsInitialized);
        Assert.False(snapshot.IsShuttingDown);
        Assert.Equal(BuiltInHudIds.Music, snapshot.CurrentHudId);
        Assert.Same(music, snapshot.CurrentModule);
        Assert.Equal(HudPresentationState.Collapsed, snapshot.PresentationState);
    }

    [Fact]
    public async Task RuntimeFallback_DoesNotMutateConfiguredIds()
    {
        var configuredIds = new List<string> { "missing" };
        var launcher = new FakeHudModule("launcher");
        var music = new FakeHudModule(BuiltInHudIds.Music);
        var router = new HudRouter(CreateRegistry(launcher, music), "missing", configuredIds);
        await router.InitializeAsync(CancellationToken.None);
        Assert.Equal(new[] { "missing" }, configuredIds);
        Assert.Equal(new[] { BuiltInHudIds.Music }, router.EnabledHudModuleIds);
        Assert.Equal(BuiltInHudIds.Music, router.EffectiveDefaultHudId);
        Assert.Same(music, router.CurrentModule);
    }

    [Fact]
    public void RuntimeConfiguration_IgnoresInvalidUnknownAndDuplicateIds()
    {
        var music = new FakeHudModule(BuiltInHudIds.Music);
        var launcher = new FakeHudModule("launcher");
        string[] configuredIds =
            [null!, "", " ", "missing", "LAUNCHER", "launcher", "launcher", "\t"];

        var router = new HudRouter(
            CreateRegistry(music, launcher),
            "launcher",
            configuredIds);

        Assert.Equal(["launcher"], router.EnabledHudModuleIds);
        Assert.Equal("launcher", router.EffectiveDefaultHudId);
    }

    [Fact]
    public void RuntimeConfiguration_WhenNoValidConfiguredIdRemainsUsesMusicFallback()
    {
        var music = new FakeHudModule(BuiltInHudIds.Music);
        var launcher = new FakeHudModule("launcher");
        string[] configuredIds = [null!, "", " ", "missing", "\r\n"];

        var router = new HudRouter(
            CreateRegistry(launcher, music),
            "missing",
            configuredIds);

        Assert.Equal([BuiltInHudIds.Music], router.EnabledHudModuleIds);
        Assert.Equal(BuiltInHudIds.Music, router.EffectiveDefaultHudId);
    }

    [Fact]
    public async Task NavigateToAsync_UnknownOrDisabledIdUsesEffectiveDefault()
    {
        var music = new FakeHudModule(BuiltInHudIds.Music);
        var launcher = new FakeHudModule("launcher");
        HudRouter router = await CreateInitializedRouterAsync(music, launcher, enabledIds: [BuiltInHudIds.Music]);
        await router.NavigateToAsync("launcher", CancellationToken.None);
        await router.NavigateToAsync("missing", CancellationToken.None);
        Assert.Same(music, router.CurrentModule);
        Assert.Equal(1, music.ActivateCount);
        Assert.Equal(0, launcher.ActivateCount);
    }

    [Fact]
    public async Task NavigateToAsync_UsesRequiredLifecycleAndNotificationOrder()
    {
        var calls = new List<string>();
        var music = new FakeHudModule(BuiltInHudIds.Music, calls);
        var launcher = new FakeHudModule("launcher", calls) { InvalidateDuringActivate = true };
        HudRouter router = await CreateInitializedRouterAsync(music, launcher);
        calls.Clear();
        router.StateChanged += (_, _) => calls.Add("router:state");
        router.PresentationChanged += (_, _) => calls.Add("router:presentation");
        await router.NavigateToAsync("launcher", CancellationToken.None);
        Assert.Equal(
            ["music:unsubscribe", "music:deactivate", "launcher:initialize", "launcher:subscribe",
                "launcher:activate", "launcher:invalidate", "router:state", "router:presentation"],
            calls);
    }

    [Fact]
    public async Task NavigateToAsync_CoalescesInvalidationsRaisedDuringActivation()
    {
        var music = new FakeHudModule(BuiltInHudIds.Music);
        var launcher = new FakeHudModule("launcher") { InvalidationsDuringActivate = 3 };
        HudRouter router = await CreateInitializedRouterAsync(music, launcher);
        int count = 0;
        router.PresentationChanged += (_, _) => count++;
        await router.NavigateToAsync("launcher", CancellationToken.None);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task NavigateToAsync_DiscardsPendingInvalidationFromPreviousSubscription()
    {
        var music = new FakeHudModule(BuiltInHudIds.Music);
        var launcher = new FakeHudModule("launcher");
        HudRouter router = await CreateInitializedRouterAsync(music, launcher);
        Task? navigation = null;
        Task? invalidationLoop = null;
        using var stopInvalidations = new CancellationTokenSource();
        int presentationChangedCount = 0;
        router.PresentationChanged += (_, _) =>
        {
            if (Interlocked.Increment(ref presentationChangedCount) == 1)
            {
                navigation = router.NavigateToAsync(launcher.Id, CancellationToken.None);
                invalidationLoop = Task.Run(() =>
                {
                    while (!stopInvalidations.IsCancellationRequested)
                    {
                        music.RaisePresentationInvalidated();
                        Thread.Yield();
                    }
                });
            }
        };

        music.RaisePresentationInvalidated();
        Task actualNavigation = Assert.IsAssignableFrom<Task>(navigation);
        try
        {
            await actualNavigation.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            stopInvalidations.Cancel();
            await Assert.IsAssignableFrom<Task>(invalidationLoop)
                .WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.Same(launcher, router.CurrentModule);
        Assert.Equal(1, presentationChangedCount);
    }

    [Fact]
    public async Task NavigateToAsync_IgnoresOldHandlerCapturedBeforeUnsubscribe()
    {
        var music = new FakeHudModule(BuiltInHudIds.Music);
        var launcher = new FakeHudModule("launcher");
        HudRouter router = await CreateInitializedRouterAsync(music, launcher);
        EventHandler? oldHandler = music.CapturePresentationInvalidatedHandlers();
        int presentationChangedCount = 0;
        router.PresentationChanged += (_, _) => presentationChangedCount++;

        await router.NavigateToAsync(launcher.Id, CancellationToken.None);
        int countAfterNavigation = presentationChangedCount;

        Assert.NotNull(oldHandler);
        oldHandler(music, EventArgs.Empty);
        Assert.Equal(countAfterNavigation, presentationChangedCount);
    }

    [Fact]
    public async Task NavigateToAsync_DrainsInvalidationsRaisedWhilePendingNotificationIsPublishing()
    {
        var notificationDrained = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var music = new FakeHudModule(BuiltInHudIds.Music);
        var launcher = new FakeHudModule("launcher") { InvalidateDuringActivate = true };
        HudRouter router = await CreateInitializedRouterAsync(music, launcher);
        int presentationChangedCount = 0;
        router.PresentationChanged += (_, _) =>
        {
            int notificationCount = Interlocked.Increment(ref presentationChangedCount);
            if (notificationCount == 2)
            {
                notificationDrained.TrySetResult();
            }

            if (notificationCount == 1)
            {
                launcher.RaisePresentationInvalidated();
                launcher.RaisePresentationInvalidated();
                launcher.RaisePresentationInvalidated();
            }
        };

        await router.NavigateToAsync(launcher.Id, CancellationToken.None);

        await notificationDrained.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, presentationChangedCount);
    }

    [Fact]
    public async Task NavigateToAsync_WhenActivationFails_RestoresOldWithoutPublishingIntermediateCurrent()
    {
        var music = new FakeHudModule(BuiltInHudIds.Music);
        var launcher = new FakeHudModule("launcher") { ActivateException = new InvalidOperationException("failed") };
        HudRouter router = await CreateInitializedRouterAsync(music, launcher);
        var observedIds = new List<string?>();
        router.StateChanged += (_, _) => observedIds.Add(router.GetSnapshot().CurrentHudId);
        HudNavigationException exception = await Assert.ThrowsAsync<HudNavigationException>(
            () => router.NavigateToAsync("launcher", CancellationToken.None));
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Same(music, router.CurrentModule);
        Assert.Equal(2, music.ActivateCount);
        Assert.Equal(1, launcher.DeactivateCount);
        Assert.Equal(new[] { BuiltInHudIds.Music }, observedIds);
    }

    [Fact]
    public async Task NavigateToAsync_WhenRecoveryStateSubscriberThrows_PreservesNavigationFailure()
    {
        var activationException = new InvalidOperationException("launcher activation failed");
        var subscriberException = new InvalidOperationException("state subscriber failed");
        var music = new FakeHudModule(BuiltInHudIds.Music);
        var launcher = new FakeHudModule("launcher") { ActivateException = activationException };
        HudRouter router = await CreateInitializedRouterAsync(music, launcher);
        int remainingSubscriberCalls = 0;
        router.StateChanged += (_, _) => throw subscriberException;
        router.StateChanged += (_, _) => remainingSubscriberCalls++;

        HudNavigationException exception = await Assert.ThrowsAsync<HudNavigationException>(
            () => router.NavigateToAsync(launcher.Id, CancellationToken.None));

        Assert.Equal(launcher.Id, exception.TargetHudId);
        Assert.Same(activationException, exception.InnerException);
        Assert.Contains(subscriberException, exception.RecoveryExceptions);
        Assert.Same(music, router.CurrentModule);
        Assert.Equal(1, remainingSubscriberCalls);
    }

    [Fact]
    public async Task NavigateToAsync_WhenRecoveryPresentationSubscriberThrows_PreservesNavigationFailure()
    {
        var activationException = new InvalidOperationException("launcher activation failed");
        var subscriberException = new InvalidOperationException("presentation subscriber failed");
        var music = new FakeHudModule(BuiltInHudIds.Music);
        var launcher = new FakeHudModule("launcher") { ActivateException = activationException };
        HudRouter router = await CreateInitializedRouterAsync(music, launcher);
        music.InvalidateDuringActivate = true;
        int remainingSubscriberCalls = 0;
        router.PresentationChanged += (_, _) => throw subscriberException;
        router.PresentationChanged += (_, _) => remainingSubscriberCalls++;

        HudNavigationException exception = await Assert.ThrowsAsync<HudNavigationException>(
            () => router.NavigateToAsync(launcher.Id, CancellationToken.None));

        Assert.Same(activationException, exception.InnerException);
        Assert.Contains(subscriberException, exception.RecoveryExceptions);
        Assert.Same(music, router.CurrentModule);
        Assert.Equal(1, remainingSubscriberCalls);
    }

    [Fact]
    public async Task NavigateToAsync_WhenOldRecoveryFails_RestoresEffectiveDefault()
    {
        var music = new FakeHudModule(BuiltInHudIds.Music);
        var old = new FakeHudModule("old");
        var target = new FakeHudModule("target");
        HudRouter router = await CreateInitializedRouterAsync(music, old, target);
        await router.NavigateToAsync("old", CancellationToken.None);
        old.ActivateException = new InvalidOperationException("old failed");
        target.ActivateException = new InvalidOperationException("target failed");
        await Assert.ThrowsAsync<HudNavigationException>(() => router.NavigateToAsync("target", CancellationToken.None));
        Assert.Same(music, router.CurrentModule);
    }

    [Fact]
    public async Task NavigateToAsync_WhenDefaultTargetAndOldRecoveryFail_RetriesDefaultForRecovery()
    {
        var navigationException = new InvalidOperationException("music navigation failed");
        var oldRecoveryException = new InvalidOperationException("old recovery failed");
        var music = new FakeHudModule(BuiltInHudIds.Music);
        var old = new FakeHudModule("old");
        HudRouter router = await CreateInitializedRouterAsync(music, old);
        await router.NavigateToAsync("old", CancellationToken.None);
        music.ActivateResults.Enqueue(navigationException);
        music.ActivateResults.Enqueue(null);
        old.ActivateException = oldRecoveryException;

        HudNavigationException exception = await Assert.ThrowsAsync<HudNavigationException>(
            () => router.NavigateToAsync("unknown", CancellationToken.None));

        Assert.Same(navigationException, exception.InnerException);
        Assert.Contains(oldRecoveryException, exception.RecoveryExceptions);
        Assert.Same(music, router.CurrentModule);
        Assert.Equal(3, music.ActivateCount);
    }

    [Fact]
    public async Task NavigateToAsync_WhenAllRecoveryFails_AllowsReinitializeAfterCauseIsRemoved()
    {
        var targetException = new InvalidOperationException("target activate failed");
        var oldRecoveryException = new InvalidOperationException("old recovery failed");
        var defaultRecoveryException = new InvalidOperationException("default recovery failed");
        var music = new FakeHudModule(BuiltInHudIds.Music);
        var old = new FakeHudModule("old");
        var target = new FakeHudModule("target");
        HudRouter router = await CreateInitializedRouterAsync(music, old, target);
        await router.NavigateToAsync(old.Id, CancellationToken.None);
        target.ActivateException = targetException;
        old.ActivateException = oldRecoveryException;
        music.ActivateException = defaultRecoveryException;

        HudNavigationException exception = await Assert.ThrowsAsync<HudNavigationException>(
            () => router.NavigateToAsync(target.Id, CancellationToken.None));
        HudRouterSnapshot failedSnapshot = router.GetSnapshot();

        Assert.Same(targetException, exception.InnerException);
        Assert.Contains(oldRecoveryException, exception.RecoveryExceptions);
        Assert.Contains(defaultRecoveryException, exception.RecoveryExceptions);
        Assert.False(failedSnapshot.IsInitialized);
        Assert.Null(failedSnapshot.CurrentHudId);
        Assert.Null(failedSnapshot.CurrentModule);
        Assert.Equal(HudPresentationState.Collapsed, failedSnapshot.PresentationState);

        music.ActivateException = null;
        int stateChangedCount = 0;
        int presentationChangedCount = 0;
        router.StateChanged += (_, _) => stateChangedCount++;
        router.PresentationChanged += (_, _) => presentationChangedCount++;
        await router.InitializeAsync(CancellationToken.None);
        music.RaisePresentationInvalidated();

        Assert.True(router.GetSnapshot().IsInitialized);
        Assert.Same(music, router.CurrentModule);
        Assert.Equal(BuiltInHudIds.Music, router.CurrentHudId);
        Assert.Equal(HudPresentationState.Collapsed, router.PresentationState);
        Assert.Equal(1, stateChangedCount);
        Assert.Equal(1, presentationChangedCount);
    }

    [Fact]
    public void Constructor_EffectiveDefaultUsesRegistryOrderBeforeConfiguredEnabledOrder()
    {
        var first = new FakeHudModule("first");
        var second = new FakeHudModule("second");

        var router = new HudRouter(
            CreateRegistry(first, second),
            "missing",
            [second.Id, first.Id]);

        Assert.Equal(first.Id, router.EffectiveDefaultHudId);
        Assert.Equal(new[] { second.Id, first.Id }, router.EnabledHudModuleIds);
    }

    [Fact]
    public async Task NavigateToAsync_WhenOldDeactivationFails_RestoresOldAndAllowsLaterNavigation()
    {
        var deactivationException = new InvalidOperationException("music deactivate failed");
        var music = new FakeHudModule(BuiltInHudIds.Music) { DeactivateException = deactivationException };
        var launcher = new FakeHudModule("launcher");
        HudRouter router = await CreateInitializedRouterAsync(music, launcher);

        HudNavigationException exception = await Assert.ThrowsAsync<HudNavigationException>(
            () => router.NavigateToAsync(launcher.Id, CancellationToken.None));

        Assert.Same(deactivationException, exception.InnerException);
        Assert.Same(music, router.CurrentModule);
        Assert.True(router.GetSnapshot().IsInitialized);
        music.DeactivateException = null;
        await router.NavigateToAsync(launcher.Id, CancellationToken.None);
        Assert.Same(launcher, router.CurrentModule);
    }

    [Fact]
    public async Task NavigateToAsync_WhenTargetInitializationFails_RestoresOldAndAllowsLaterNavigation()
    {
        var initializeException = new InvalidOperationException("launcher initialize failed");
        var music = new FakeHudModule(BuiltInHudIds.Music);
        var launcher = new FakeHudModule("launcher") { InitializeException = initializeException };
        HudRouter router = await CreateInitializedRouterAsync(music, launcher);

        HudNavigationException exception = await Assert.ThrowsAsync<HudNavigationException>(
            () => router.NavigateToAsync(launcher.Id, CancellationToken.None));

        Assert.Same(initializeException, exception.InnerException);
        Assert.Same(music, router.CurrentModule);
        launcher.InitializeException = null;
        await router.NavigateToAsync(launcher.Id, CancellationToken.None);
        Assert.Same(launcher, router.CurrentModule);
    }

    [Fact]
    public async Task NavigateToAsync_WhenTargetSubscriptionFails_RestoresOld()
    {
        var subscribeException = new InvalidOperationException("launcher subscribe failed");
        var music = new FakeHudModule(BuiltInHudIds.Music);
        var launcher = new FakeHudModule("launcher") { SubscribeException = subscribeException };
        HudRouter router = await CreateInitializedRouterAsync(music, launcher);
        int presentationCount = 0;
        router.PresentationChanged += (_, _) => presentationCount++;

        HudNavigationException exception = await Assert.ThrowsAsync<HudNavigationException>(
            () => router.NavigateToAsync(launcher.Id, CancellationToken.None));

        Assert.Same(subscribeException, exception.InnerException);
        Assert.Same(music, router.CurrentModule);
        music.RaisePresentationInvalidated();
        Assert.Equal(1, presentationCount);
    }

    [Fact]
    public async Task NavigateToAsync_WhenTargetInitializationIsCancelled_RestoresOld()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var neverCompletes = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellationSource = new CancellationTokenSource();
        var music = new FakeHudModule(BuiltInHudIds.Music);
        var launcher = new FakeHudModule("launcher")
        {
            InitializeStartedSignal = started,
            InitializeWaitTask = neverCompletes.Task
        };
        HudRouter router = await CreateInitializedRouterAsync(music, launcher);

        Task navigation = router.NavigateToAsync(launcher.Id, cancellationSource.Token);
        await started.Task;
        cancellationSource.Cancel();
        HudNavigationException exception = await Assert.ThrowsAsync<HudNavigationException>(() => navigation);

        Assert.IsAssignableFrom<OperationCanceledException>(exception.InnerException);
        Assert.Same(music, router.CurrentModule);
        Assert.True(router.GetSnapshot().IsInitialized);
    }

    [Fact]
    public async Task NavigateToAsync_WhenOldDeactivationIsCancelled_RestoresOld()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var neverCompletes = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellationSource = new CancellationTokenSource();
        var music = new FakeHudModule(BuiltInHudIds.Music)
        {
            DeactivateStartedSignal = started,
            DeactivateWaitTask = neverCompletes.Task
        };
        var launcher = new FakeHudModule("launcher");
        HudRouter router = await CreateInitializedRouterAsync(music, launcher);

        Task navigation = router.NavigateToAsync(launcher.Id, cancellationSource.Token);
        await started.Task;
        cancellationSource.Cancel();
        HudNavigationException exception = await Assert.ThrowsAsync<HudNavigationException>(() => navigation);

        Assert.IsAssignableFrom<OperationCanceledException>(exception.InnerException);
        Assert.Same(music, router.CurrentModule);
        Assert.True(router.GetSnapshot().IsInitialized);
    }

    [Fact]
    public async Task InitializeAsync_WhenActivationAndCleanupFail_PreservesBothExceptions()
    {
        var activationException = new InvalidOperationException("music activate failed");
        var cleanupException = new InvalidOperationException("music cleanup failed");
        var music = new FakeHudModule(BuiltInHudIds.Music)
        {
            ActivateException = activationException,
            DeactivateException = cleanupException
        };
        var router = new HudRouter(CreateRegistry(music), BuiltInHudIds.Music, [BuiltInHudIds.Music]);

        HudNavigationException exception = await Assert.ThrowsAsync<HudNavigationException>(
            () => router.InitializeAsync(CancellationToken.None));

        Assert.Same(activationException, exception.InnerException);
        Assert.Contains(cleanupException, exception.RecoveryExceptions);
        Assert.False(router.GetSnapshot().IsInitialized);
        Assert.Null(router.CurrentModule);
    }

    [Fact]
    public async Task InitializeAsync_WhenActivationFailsAndCleanupSucceeds_RethrowsOriginalException()
    {
        var activationException = new InvalidOperationException("music activate failed");
        var music = new FakeHudModule(BuiltInHudIds.Music) { ActivateException = activationException };
        var router = new HudRouter(CreateRegistry(music), BuiltInHudIds.Music, [BuiltInHudIds.Music]);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => router.InitializeAsync(CancellationToken.None));

        Assert.Same(activationException, exception);
    }

    [Fact]
    public async Task NavigateToAsync_WhenOldUnsubscribeFails_RestoresOldAndAllowsLaterNavigation()
    {
        var unsubscribeException = new InvalidOperationException("music unsubscribe failed");
        var music = new FakeHudModule(BuiltInHudIds.Music)
        {
            UnsubscribeException = unsubscribeException,
            UnsubscribeExceptionOnce = true
        };
        var launcher = new FakeHudModule("launcher");
        HudRouter router = await CreateInitializedRouterAsync(music, launcher);
        int presentationCount = 0;
        router.PresentationChanged += (_, _) => presentationCount++;

        HudNavigationException exception = await Assert.ThrowsAsync<HudNavigationException>(
            () => router.NavigateToAsync(launcher.Id, CancellationToken.None));

        Assert.Same(unsubscribeException, exception.InnerException);
        Assert.Same(music, router.CurrentModule);
        Assert.True(router.GetSnapshot().IsInitialized);
        Assert.Equal(2, music.PresentationUnsubscribeCount);
        Assert.Equal(1, music.PresentationHandlerCount);
        Assert.True(router.SetPresentationState(HudPresentationState.Expanded));
        music.RaisePresentationInvalidated();
        Assert.Equal(1, presentationCount);
        await router.NavigateToAsync(launcher.Id, CancellationToken.None);
        Assert.Same(launcher, router.CurrentModule);
        Assert.Equal(0, music.PresentationHandlerCount);
    }

    [Fact]
    public async Task NavigateToAsync_WhenTargetSubscribeAndCleanupFail_RemovesStaleTargetBeforeRetry()
    {
        var subscribeException = new InvalidOperationException("launcher subscribe failed after add");
        var cleanupException = new InvalidOperationException("launcher cleanup failed");
        var music = new FakeHudModule(BuiltInHudIds.Music);
        var launcher = new FakeHudModule("launcher")
        {
            SubscribeAfterAddException = subscribeException,
            UnsubscribeException = cleanupException,
            UnsubscribeExceptionOnce = true
        };
        HudRouter router = await CreateInitializedRouterAsync(music, launcher);

        HudNavigationException exception = await Assert.ThrowsAsync<HudNavigationException>(
            () => router.NavigateToAsync(launcher.Id, CancellationToken.None));
        launcher.SubscribeAfterAddException = null;
        await router.NavigateToAsync(launcher.Id, CancellationToken.None);

        Assert.Same(subscribeException, exception.InnerException);
        Assert.Contains(cleanupException, exception.RecoveryExceptions);
        Assert.Equal(2, launcher.PresentationUnsubscribeCount);
        Assert.Equal(1, launcher.PresentationHandlerCount);

        await router.NavigateToAsync(music.Id, CancellationToken.None);

        Assert.Equal(0, launcher.PresentationHandlerCount);
    }

    [Fact]
    public async Task ShutdownAsync_WhenUnsubscribeAndDeactivateFail_FinalizesStateAndAggregatesFailures()
    {
        var unsubscribeException = new InvalidOperationException("music unsubscribe failed");
        var deactivateException = new InvalidOperationException("music deactivate failed");
        var music = new FakeHudModule(BuiltInHudIds.Music)
        {
            UnsubscribeException = unsubscribeException,
            DeactivateException = deactivateException
        };
        HudRouter router = await CreateInitializedRouterAsync(music);
        int presentationCount = 0;
        router.PresentationChanged += (_, _) => presentationCount++;

        AggregateException exception = await Assert.ThrowsAsync<AggregateException>(
            () => router.ShutdownAsync(CancellationToken.None));
        await router.ShutdownAsync(CancellationToken.None);
        music.RaisePresentationInvalidated();

        Assert.Equal(new Exception[] { unsubscribeException, deactivateException }, exception.InnerExceptions);
        Assert.Equal(1, music.DeactivateCount);
        Assert.Equal(0, presentationCount);
        HudRouterSnapshot snapshot = router.GetSnapshot();
        Assert.Null(snapshot.CurrentHudId);
        Assert.Null(snapshot.CurrentModule);
        Assert.False(snapshot.IsInitialized);
        Assert.True(snapshot.IsShuttingDown);
    }

    [Fact]
    public async Task ShutdownAsync_WhenCleanupAndStateSubscriberFail_AggregatesAllFailures()
    {
        var unsubscribeException = new InvalidOperationException("music unsubscribe failed");
        var deactivateException = new InvalidOperationException("music deactivate failed");
        var subscriberException = new InvalidOperationException("state subscriber failed");
        var music = new FakeHudModule(BuiltInHudIds.Music)
        {
            UnsubscribeException = unsubscribeException,
            DeactivateException = deactivateException
        };
        HudRouter router = await CreateInitializedRouterAsync(music);
        int remainingSubscriberCalls = 0;
        router.StateChanged += (_, _) => throw subscriberException;
        router.StateChanged += (_, _) => remainingSubscriberCalls++;

        AggregateException exception = await Assert.ThrowsAsync<AggregateException>(
            () => router.ShutdownAsync(CancellationToken.None));
        await router.ShutdownAsync(CancellationToken.None);

        Assert.Equal(
            new Exception[] { unsubscribeException, deactivateException, subscriberException },
            exception.InnerExceptions);
        Assert.Equal(1, remainingSubscriberCalls);
        Assert.Equal(1, music.DeactivateCount);
        HudRouterSnapshot snapshot = router.GetSnapshot();
        Assert.Null(snapshot.CurrentHudId);
        Assert.Null(snapshot.CurrentModule);
        Assert.False(snapshot.IsInitialized);
        Assert.True(snapshot.IsShuttingDown);
    }

    [Fact]
    public async Task InitializeAsync_WhenSubscribeAddsThenThrows_RemovesHandlerDuringCleanup()
    {
        var subscribeException = new InvalidOperationException("music subscribe failed after add");
        var music = new FakeHudModule(BuiltInHudIds.Music)
        {
            SubscribeAfterAddException = subscribeException
        };
        var router = new HudRouter(CreateRegistry(music), BuiltInHudIds.Music, [BuiltInHudIds.Music]);
        int presentationCount = 0;
        router.PresentationChanged += (_, _) => presentationCount++;

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => router.InitializeAsync(CancellationToken.None));
        music.RaisePresentationInvalidated();

        Assert.Same(subscribeException, exception);
        Assert.Equal(1, music.DeactivateCount);
        Assert.Equal(0, presentationCount);
        Assert.False(router.GetSnapshot().IsInitialized);
    }

    [Fact]
    public async Task DisableAsync_CurrentModuleSelectsFallbackAndLastEnabledAddsMusicAtRuntime()
    {
        var configuredIds = new List<string> { BuiltInHudIds.Music, "launcher" };
        var music = new FakeHudModule(BuiltInHudIds.Music);
        var launcher = new FakeHudModule("launcher");
        var router = new HudRouter(CreateRegistry(music, launcher), "launcher", configuredIds);
        await router.InitializeAsync(CancellationToken.None);
        await router.DisableAsync("launcher", CancellationToken.None);
        await router.DisableAsync(BuiltInHudIds.Music, CancellationToken.None);
        Assert.Equal(new[] { BuiltInHudIds.Music, "launcher" }, configuredIds);
        Assert.Equal(new[] { BuiltInHudIds.Music }, router.EnabledHudModuleIds);
        Assert.Same(music, router.CurrentModule);
    }

    [Fact]
    public async Task NavigateToAsync_ConcurrentCallsAreSerialized()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var music = new FakeHudModule(BuiltInHudIds.Music);
        var first = new FakeHudModule("first") { ActivateStartedSignal = started, ActivateWaitTask = release.Task };
        var second = new FakeHudModule("second");
        HudRouter router = await CreateInitializedRouterAsync(music, first, second);
        Task firstNavigation = router.NavigateToAsync("first", CancellationToken.None);
        await started.Task;
        Task secondNavigation = router.NavigateToAsync("second", CancellationToken.None);
        bool secondStartedBeforeRelease = second.ActivateCount > 0;
        release.SetResult(true);
        await Task.WhenAll(firstNavigation, secondNavigation);
        Assert.False(secondStartedBeforeRelease);
        Assert.Same(second, router.CurrentModule);
    }

    [Fact]
    public async Task TransitionInProgress_RejectsPresentationChangesAndDoesNotNotifyState()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var music = new FakeHudModule(BuiltInHudIds.Music);
        var launcher = new FakeHudModule("launcher") { ActivateStartedSignal = started, ActivateWaitTask = release.Task };
        HudRouter router = await CreateInitializedRouterAsync(music, launcher);
        int count = 0;
        router.StateChanged += (_, _) => count++;
        Task navigation = router.NavigateToAsync("launcher", CancellationToken.None);
        await started.Task;
        Assert.False(router.SetPresentationState(HudPresentationState.Expanded));
        Assert.False(router.CollapseFromPointerLeave());
        Assert.Equal(0, count);
        release.SetResult(true);
        await navigation;
    }

    [Fact]
    public async Task NavigateToAsync_WaitsUntilPresentationStateNotificationCompletes()
    {
        var deactivateStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var music = new FakeHudModule(BuiltInHudIds.Music)
        {
            DeactivateStartedSignal = deactivateStarted
        };
        var launcher = new FakeHudModule("launcher");
        HudRouter router = await CreateInitializedRouterAsync(music, launcher);
        Task? navigation = null;
        bool deactivatedDuringNotification = false;
        router.StateChanged += (_, _) =>
        {
            HudRouterSnapshot snapshot = router.GetSnapshot();
            if (!ReferenceEquals(snapshot.CurrentModule, music)
                || snapshot.PresentationState != HudPresentationState.Expanded)
            {
                return;
            }

            navigation = router.NavigateToAsync(launcher.Id, CancellationToken.None);
            deactivatedDuringNotification = deactivateStarted.Task.IsCompleted;
        };

        Assert.True(router.SetPresentationState(HudPresentationState.Expanded));
        Assert.NotNull(navigation);
        Assert.False(deactivatedDuringNotification);
        await navigation;

        Assert.Same(launcher, router.CurrentModule);
    }

    [Fact]
    public async Task SetPresentationState_DoesNotPublishDuringNavigationStateNotification()
    {
        var music = new FakeHudModule(BuiltInHudIds.Music);
        var launcher = new FakeHudModule("launcher");
        HudRouter router = await CreateInitializedRouterAsync(music, launcher);
        bool? changedDuringNotification = null;
        HudPresentationState? stateDuringNotification = null;
        router.StateChanged += (_, _) =>
        {
            if (!ReferenceEquals(router.GetSnapshot().CurrentModule, launcher))
            {
                return;
            }

            changedDuringNotification = router.SetPresentationState(
                HudPresentationState.Expanded);
            stateDuringNotification = router.PresentationState;
        };

        await router.NavigateToAsync(launcher.Id, CancellationToken.None);

        Assert.False(changedDuringNotification);
        Assert.Equal(HudPresentationState.Collapsed, stateDuringNotification);
        Assert.True(router.SetPresentationState(HudPresentationState.Expanded));
    }

    [Fact]
    public async Task CollapseFromPointerLeave_DoesNotPublishDuringNavigationStateNotification()
    {
        var music = new FakeHudModule(BuiltInHudIds.Music);
        var launcher = new FakeHudModule("launcher");
        HudRouter router = await CreateInitializedRouterAsync(music, launcher);
        Assert.True(router.SetPresentationState(HudPresentationState.Expanded));
        bool? changedDuringNotification = null;
        HudPresentationState? stateDuringNotification = null;
        router.StateChanged += (_, _) =>
        {
            if (!ReferenceEquals(router.GetSnapshot().CurrentModule, launcher))
            {
                return;
            }

            changedDuringNotification = router.CollapseFromPointerLeave();
            stateDuringNotification = router.PresentationState;
        };

        await router.NavigateToAsync(launcher.Id, CancellationToken.None);

        Assert.False(changedDuringNotification);
        Assert.Equal(HudPresentationState.Expanded, stateDuringNotification);
        Assert.True(router.CollapseFromPointerLeave());
    }

    [Fact]
    public async Task SamePresentationState_DoesNotNotifyAndEventHandlersCanReadSnapshot()
    {
        var music = new FakeHudModule(BuiltInHudIds.Music);
        HudRouter router = await CreateInitializedRouterAsync(music);
        HudRouterSnapshot? stateSnapshot = null;
        HudRouterSnapshot? presentationSnapshot = null;
        router.StateChanged += (_, _) => stateSnapshot = router.GetSnapshot();
        router.PresentationChanged += (_, _) => presentationSnapshot = router.GetSnapshot();
        Assert.False(router.SetPresentationState(HudPresentationState.Collapsed));
        Assert.Null(stateSnapshot);
        Assert.True(router.SetPresentationState(HudPresentationState.Expanded));
        music.RaisePresentationInvalidated();
        Assert.Equal(HudPresentationState.Expanded, stateSnapshot?.PresentationState);
        Assert.Same(music, presentationSnapshot?.CurrentModule);
    }

    [Fact]
    public async Task ShutdownAsync_UnsubscribesThenDeactivatesOnceAndRejectsFurtherChanges()
    {
        var calls = new List<string>();
        var music = new FakeHudModule(BuiltInHudIds.Music, calls);
        HudRouter router = await CreateInitializedRouterAsync(music);
        calls.Clear();
        int stateCount = 0;
        int presentationCount = 0;
        router.StateChanged += (_, _) => stateCount++;
        router.PresentationChanged += (_, _) => presentationCount++;
        await router.ShutdownAsync(CancellationToken.None);
        await router.ShutdownAsync(CancellationToken.None);
        Assert.False(router.SetPresentationState(HudPresentationState.Expanded));
        Assert.False(router.CollapseFromPointerLeave());
        music.RaisePresentationInvalidated();
        Assert.Equal(["music:unsubscribe", "music:deactivate", "music:invalidate"], calls);
        Assert.Equal(1, stateCount);
        Assert.Equal(0, presentationCount);
        HudRouterSnapshot snapshot = router.GetSnapshot();
        Assert.Null(snapshot.CurrentModule);
        Assert.False(snapshot.IsInitialized);
        Assert.True(snapshot.IsShuttingDown);
    }

    private static HudRegistry CreateRegistry(params FakeHudModule[] modules)
    {
        var registry = new HudRegistry();
        foreach (FakeHudModule module in modules)
        {
            registry.Register(module);
        }
        return registry;
    }

    private static async Task<HudRouter> CreateInitializedRouterAsync(
        FakeHudModule music,
        FakeHudModule? second = null,
        FakeHudModule? third = null,
        string defaultId = BuiltInHudIds.Music,
        IReadOnlyList<string>? enabledIds = null)
    {
        FakeHudModule[] modules = new[] { music, second, third }.OfType<FakeHudModule>().ToArray();
        enabledIds ??= modules.Select(module => module.Id).ToArray();
        var router = new HudRouter(CreateRegistry(modules), defaultId, enabledIds);
        await router.InitializeAsync(CancellationToken.None);
        return router;
    }
}
