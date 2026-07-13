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

    private static async Task<HudRouter> CreateInitializedRouterAsync(
        FakeHudModule music)
    {
        var registry = new HudRegistry();
        registry.Register(music);
        var router = new HudRouter(
            registry,
            BuiltInHudIds.Music,
            [BuiltInHudIds.Music]);
        await router.InitializeAsync(CancellationToken.None);
        return router;
    }
}
