using System.Windows;
using NoraBar.Hud;
using NoraBar.Hud.Home;
using NoraBar.Models;
using Xunit;

namespace NoraBar.Tests.Hud;

public sealed class HomeHudModuleTests
{
    [Fact]
    public void GetView_CachesOneViewPerDesign()
    {
        RunInSta(() =>
        {
            var source = new FakeHomeHudPresentationSource();
            var created = new List<HomeHudDesignVariant>();
            var module = new HomeHudModule(source, variant =>
            {
                created.Add(variant);
                return new FrameworkElement();
            });

            FrameworkElement first = module.GetView(new HudViewContext(HudPresentationState.Expanded));
            FrameworkElement second = module.GetView(new HudViewContext(HudPresentationState.Pinned));
            source.DesignVariant = HomeHudDesignVariant.FusionExpressive;
            FrameworkElement third = module.GetView(new HudViewContext(HudPresentationState.Expanded));

            Assert.Same(first, second);
            Assert.NotSame(first, third);
            Assert.Equal(
                [HomeHudDesignVariant.FusionBalanced, HomeHudDesignVariant.FusionExpressive],
                created);
        });
    }

    [Fact]
    public async Task Lifecycle_StartsAndStopsClockIdempotently()
    {
        var source = new FakeHomeHudPresentationSource();
        var module = new HomeHudModule(source, _ => new FrameworkElement());

        await module.InitializeAsync(CancellationToken.None);
        await module.InitializeAsync(CancellationToken.None);
        await module.ActivateAsync(CancellationToken.None);
        await module.ActivateAsync(CancellationToken.None);
        await module.DeactivateAsync(CancellationToken.None);
        await module.DeactivateAsync(CancellationToken.None);
        await module.DisposeAsync();
        await module.DisposeAsync();

        Assert.Equal(1, source.InitializeCount);
        Assert.Equal(1, source.StartCount);
        Assert.Equal(1, source.StopCount);
        Assert.Equal(1, source.DisposeCount);
    }

    [Fact]
    public async Task SourceInvalidation_IsForwardedOnlyAfterInitialization()
    {
        var source = new FakeHomeHudPresentationSource();
        var module = new HomeHudModule(source, _ => new FrameworkElement());
        int count = 0;
        module.PresentationInvalidated += (_, _) => count++;

        source.RaisePresentationInvalidated();
        await module.InitializeAsync(CancellationToken.None);
        source.RaisePresentationInvalidated();
        await module.DisposeAsync();
        source.RaisePresentationInvalidated();

        Assert.Equal(1, count);
    }

    private static void RunInSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception caught)
            {
                exception = caught;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exception is not null)
        {
            throw exception;
        }
    }

    private sealed class FakeHomeHudPresentationSource : IHomeHudPresentationSource
    {
        public HomeHudDesignVariant DesignVariant { get; set; } =
            HomeHudDesignVariant.FusionBalanced;

        public IReadOnlyList<NoraBar.Hud.Home.Widgets.HomeWidgetConfig>? ActiveWidgets { get; set; }

        public object ViewDataContext { get; } = new();
        public int InitializeCount { get; private set; }
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public int DisposeCount { get; private set; }

        public event EventHandler? PresentationInvalidated;

        public void Initialize() => InitializeCount++;

        public void Start() => StartCount++;

        public void Stop() => StopCount++;

        public void Dispose() => DisposeCount++;

        public void RaisePresentationInvalidated() =>
            PresentationInvalidated?.Invoke(this, EventArgs.Empty);
    }
}
