using System.Windows;
using NoraBar.Hud;
using NoraBar.Hud.Launcher;
using Xunit;

namespace NoraBar.Tests.Hud.Launcher;

public sealed class LauncherHudModuleTests
{
    [Fact]
    public async Task Lifecycle_IsIdempotentAndDisposalRemovesCallbacks()
    {
        var source = new FakeSource();
        var module = new LauncherHudModule(source, () => null!);
        int invalidations = 0;
        module.PresentationInvalidated += (_, _) => invalidations++;

        await module.InitializeAsync(CancellationToken.None);
        await module.InitializeAsync(CancellationToken.None);
        await module.ActivateAsync(CancellationToken.None);
        await module.ActivateAsync(CancellationToken.None);
        source.RaiseInvalidated();
        await module.DeactivateAsync(CancellationToken.None);
        await module.DeactivateAsync(CancellationToken.None);
        await module.DisposeAsync();
        await module.DisposeAsync();
        source.RaiseInvalidated();

        Assert.Equal(1, source.InitializeCount);
        Assert.Equal(1, source.StartCount);
        Assert.Equal(1, source.StopCount);
        Assert.Equal(1, source.DisposeCount);
        Assert.Equal(1, invalidations);
    }

    [Fact]
    public void GetView_ReusesViewAndUsesStateSpecificSizes()
    {
        StaTestRunner.Run(() =>
        {
            var source = new FakeSource();
            var view = new FrameworkElement();
            var module = new LauncherHudModule(source, () => view);

            FrameworkElement peek = module.GetView(new HudViewContext(HudPresentationState.Peek));
            FrameworkElement expanded = module.GetView(new HudViewContext(HudPresentationState.Expanded));

            Assert.Same(peek, expanded);
            Assert.Same(source, view.DataContext);
            Assert.True(module.GetPreferredSize(new HudViewContext(HudPresentationState.Peek)).Height
                < module.GetPreferredSize(new HudViewContext(HudPresentationState.Expanded)).Height);
            ValueTask dispose = module.DisposeAsync();
            Assert.True(dispose.IsCompletedSuccessfully);
        });
    }

    private sealed class FakeSource : ILauncherHudPresentationSource
    {
        public int InitializeCount { get; private set; }
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public int DisposeCount { get; private set; }
        public object ViewDataContext => this;
        public event EventHandler? PresentationInvalidated;
        public void Initialize() => InitializeCount++;
        public void Start() => StartCount++;
        public void Stop() => StopCount++;
        public void Dispose() => DisposeCount++;
        public void RaiseInvalidated() => PresentationInvalidated?.Invoke(this, EventArgs.Empty);
    }
}
