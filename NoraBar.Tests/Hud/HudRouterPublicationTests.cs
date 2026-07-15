using NoraBar.Hud;
using Xunit;

namespace NoraBar.Tests.Hud;

public sealed class HudRouterPublicationTests
{
    [Fact]
    public async Task SetPresentationState_AllowsStateSubscriberToReenterSynchronously()
    {
        var music = new FakeHudModule(BuiltInHudIds.Music);
        HudRouter router = await CreateInitializedRouterAsync(music);
        int stateChangedCount = 0;
        bool reentrantChangeSucceeded = false;
        router.StateChanged += (_, _) =>
        {
            if (Interlocked.Increment(ref stateChangedCount) == 1)
            {
                reentrantChangeSucceeded = router.SetPresentationState(
                    HudPresentationState.Pinned);
            }
        };

        bool initialChangeSucceeded = router.SetPresentationState(
            HudPresentationState.Expanded);

        Assert.True(initialChangeSucceeded);
        Assert.True(reentrantChangeSucceeded);
        Assert.Equal(2, stateChangedCount);
        Assert.Equal(HudPresentationState.Pinned, router.PresentationState);
    }

    [Fact]
    public async Task SetPresentationState_ReleasesGateBeforeSlowSubscriberCompletes()
    {
        var music = new FakeHudModule(BuiltInHudIds.Music);
        HudRouter router = await CreateInitializedRouterAsync(music);
        using var subscriberEntered = new ManualResetEventSlim();
        using var releaseSubscriber = new ManualResetEventSlim();
        int stateChangedCount = 0;
        router.StateChanged += (_, _) =>
        {
            if (Interlocked.Increment(ref stateChangedCount) == 1)
            {
                subscriberEntered.Set();
                Assert.True(releaseSubscriber.Wait(TimeSpan.FromSeconds(5)));
            }
        };

        Task<bool> firstChange = Task.Run(() =>
            router.SetPresentationState(HudPresentationState.Expanded));
        Assert.True(subscriberEntered.Wait(TimeSpan.FromSeconds(5)));

        bool concurrentChangeSucceeded = router.SetPresentationState(
            HudPresentationState.Pinned);
        releaseSubscriber.Set();

        Assert.True(await firstChange.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(concurrentChangeSucceeded);
        Assert.Equal(2, stateChangedCount);
        Assert.Equal(HudPresentationState.Pinned, router.PresentationState);
    }

    [Fact]
    public async Task PresentationChanged_AllowsSubscriberToChangeStateSynchronously()
    {
        var music = new FakeHudModule(BuiltInHudIds.Music);
        HudRouter router = await CreateInitializedRouterAsync(music);
        bool stateChangeSucceeded = false;
        router.PresentationChanged += (_, _) =>
        {
            stateChangeSucceeded = router.SetPresentationState(
                HudPresentationState.Expanded);
        };

        music.RaisePresentationInvalidated();

        Assert.True(stateChangeSucceeded);
        Assert.Equal(HudPresentationState.Expanded, router.PresentationState);
    }

    [Fact]
    public async Task NavigateToAsync_LifecyclePresentationChangedRejectsPresentationChangesUntilPublicationCompletes()
    {
        var music = new FakeHudModule(BuiltInHudIds.Music);
        var launcher = new FakeHudModule("launcher") { InvalidateDuringActivate = true };
        HudRouter router = await CreateInitializedRouterAsync(music, launcher);
        Assert.True(router.SetPresentationState(HudPresentationState.Expanded));
        bool? collapsed = null;
        bool? pinned = null;
        router.PresentationChanged += (_, _) =>
        {
            collapsed = router.CollapseFromPointerLeave();
            pinned = router.SetPresentationState(HudPresentationState.Pinned);
        };

        await router.NavigateToAsync(launcher.Id, CancellationToken.None);

        Assert.False(collapsed);
        Assert.False(pinned);
        Assert.Equal(HudPresentationState.Expanded, router.PresentationState);
    }

    [Fact]
    public async Task SetPresentationState_NoOpNavigationFromEarlierSubscriberDoesNotBlockLaterSubscriber()
    {
        var music = new FakeHudModule(BuiltInHudIds.Music);
        HudRouter router = await CreateInitializedRouterAsync(music);
        Task? navigation = null;
        bool requestNavigation = true;
        bool laterChangeSucceeded = false;
        router.StateChanged += (_, _) =>
        {
            if (requestNavigation)
            {
                requestNavigation = false;
                navigation = router.NavigateToAsync(music.Id, CancellationToken.None);
            }
        };
        router.StateChanged += (_, _) =>
        {
            if (router.PresentationState == HudPresentationState.Expanded)
            {
                laterChangeSucceeded = router.SetPresentationState(
                    HudPresentationState.Pinned);
            }
        };

        bool initialChangeSucceeded = router.SetPresentationState(
            HudPresentationState.Expanded);
        await Assert.IsAssignableFrom<Task>(navigation);

        Assert.True(initialChangeSucceeded);
        Assert.True(laterChangeSucceeded);
        Assert.Equal(HudPresentationState.Pinned, router.PresentationState);
    }

    [Fact]
    public async Task SetPresentationState_WhenSubscriberThrows_NotifiesRemainingSubscribersAndReleasesGate()
    {
        var music = new FakeHudModule(BuiltInHudIds.Music);
        HudRouter router = await CreateInitializedRouterAsync(music);
        int remainingSubscriberCalls = 0;
        EventHandler throwingSubscriber = (_, _) =>
            throw new InvalidOperationException("subscriber failed");
        router.StateChanged += throwingSubscriber;
        router.StateChanged += (_, _) => remainingSubscriberCalls++;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => router.SetPresentationState(HudPresentationState.Expanded));

        Assert.Equal("subscriber failed", exception.Message);
        Assert.Equal(1, remainingSubscriberCalls);
        router.StateChanged -= throwingSubscriber;
        Assert.True(router.SetPresentationState(HudPresentationState.Pinned));
    }

    [Fact]
    public async Task PresentationChanged_WhenDeferredDrainSubscriberThrows_ReportsFailure()
    {
        var reportedFailure = new TaskCompletionSource<Exception>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var music = new FakeHudModule(BuiltInHudIds.Music);
        var registry = new HudRegistry();
        registry.Register(music);
        var router = new HudRouter(
            registry,
            BuiltInHudIds.Music,
            [BuiltInHudIds.Music],
            exception => reportedFailure.TrySetResult(exception));
        await router.InitializeAsync(CancellationToken.None);
        var expectedFailure = new InvalidOperationException("deferred subscriber failed");
        int notificationCount = 0;
        router.PresentationChanged += (_, _) =>
        {
            if (Interlocked.Increment(ref notificationCount) == 1)
            {
                music.RaisePresentationInvalidated();
                music.RaisePresentationInvalidated();
                return;
            }

            throw expectedFailure;
        };

        music.RaisePresentationInvalidated();

        Exception actualFailure = await reportedFailure.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Same(expectedFailure, actualFailure);
        Assert.Equal(2, notificationCount);
    }

    [Fact]
    public async Task PresentationChanged_DefersRepeatedInvalidationUntilOriginatingRaiseReturns()
    {
        var music = new FakeHudModule(BuiltInHudIds.Music);
        HudRouter router = await CreateInitializedRouterAsync(music);
        var notificationsDrained = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int notificationCount = 0;
        router.PresentationChanged += (_, _) =>
        {
            int currentCount = Interlocked.Increment(ref notificationCount);
            if (currentCount < 3)
            {
                music.RaisePresentationInvalidated();
            }
            else
            {
                notificationsDrained.TrySetResult();
            }
        };

        music.RaisePresentationInvalidated();

        Assert.Equal(1, notificationCount);
        await notificationsDrained.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(3, notificationCount);
    }

    private static async Task<HudRouter> CreateInitializedRouterAsync(
        FakeHudModule music,
        params FakeHudModule[] additionalModules)
    {
        var registry = new HudRegistry();
        registry.Register(music);
        foreach (FakeHudModule module in additionalModules)
        {
            registry.Register(module);
        }

        var router = new HudRouter(
            registry,
            BuiltInHudIds.Music,
            [BuiltInHudIds.Music, .. additionalModules.Select(module => module.Id)]);
        await router.InitializeAsync(CancellationToken.None);
        return router;
    }
}
