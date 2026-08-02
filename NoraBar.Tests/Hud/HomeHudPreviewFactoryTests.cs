using System.Windows;
using System.Windows.Threading;
using NoraBar.Hud;
using NoraBar.Hud.Home;
using NoraBar.Hud.Home.Widgets;
using NoraBar.Models;
using Xunit;

namespace NoraBar.Tests.Hud;

public sealed class HomeHudPreviewFactoryTests
{
    [Fact]
    public void Constructor_WhenViewIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new HomeHudPreview(null!, new HudSize(100, 100), new TrackingSource()));
    }

    [Fact]
    public void Constructor_WhenViewModelIsNull_Throws()
    {
        StaTestRunner.Run(() => Assert.Throws<ArgumentNullException>(() =>
            new HomeHudPreview(new FrameworkElement(), new HudSize(100, 100), null!)));
    }
    [Fact]
    public void Create_WhenViewCreationFails_DisposesInitializedSource()
    {
        StaTestRunner.Run(() =>
        {
            var source = new TrackingSource();
            var failure = new InvalidOperationException("view");

            Exception exception = Assert.Throws<InvalidOperationException>(() =>
                CreatePreview(source, () => throw failure));

            Assert.Same(failure, exception);
            Assert.Equal(1, source.InitializeCount);
            Assert.Equal(0, source.StartCount);
            Assert.Equal(1, source.DisposeCount);
        });
    }

    [Fact]
    public void Create_WhenDataContextAssignmentFails_DisposesViewAndSourceBeforeStart()
    {
        StaTestRunner.Run(() =>
        {
            var source = new TrackingSource();
            var view = new TrackingView();
            var failure = new InvalidOperationException("data context");

            Exception exception = Assert.Throws<InvalidOperationException>(() =>
                CreatePreview(source, () => view, (_, _) => throw failure));

            Assert.Same(failure, exception);
            Assert.Equal(1, view.DisposeCount);
            Assert.Equal(0, source.StartCount);
            Assert.Equal(1, source.DisposeCount);
        });
    }

    [Fact]
    public void Create_WhenLayoutFails_DisposesViewAndSourceBeforeStart()
    {
        StaTestRunner.Run(() =>
        {
            var source = new TrackingSource();
            var view = new TrackingView();
            var failure = new InvalidOperationException("layout");

            Exception exception = Assert.Throws<InvalidOperationException>(() =>
                CreatePreview(
                    source,
                    () => view,
                    calculateLayout: (_, _, _) => throw failure));

            Assert.Same(failure, exception);
            Assert.Equal(1, view.DisposeCount);
            Assert.Equal(0, source.StartCount);
            Assert.Equal(1, source.DisposeCount);
        });
    }

    [Fact]
    public void Create_WhenFinalPreviewConstructionFails_CleansStartedResources()
    {
        StaTestRunner.Run(() =>
        {
            var source = new TrackingSource();
            var view = new TrackingView();
            var failure = new InvalidOperationException("preview");

            Exception exception = Assert.Throws<InvalidOperationException>(() =>
                CreatePreview(
                    source,
                    () => view,
                    createPreview: (_, _, _) => throw failure));

            Assert.Same(failure, exception);
            Assert.Equal(1, source.StartCount);
            Assert.Equal(1, view.DisposeCount);
            Assert.Equal(1, source.DisposeCount);
        });
    }

    [Fact]
    public void Create_WhenCleanupAlsoFails_PreservesCreationFailureFirst()
    {
        StaTestRunner.Run(() =>
        {
            var sourceFailure = new InvalidOperationException("source cleanup");
            var viewFailure = new InvalidOperationException("view cleanup");
            var creationFailure = new InvalidOperationException("preview");
            var source = new TrackingSource { DisposeException = sourceFailure };
            var view = new TrackingView { DisposeException = viewFailure };

            AggregateException exception = Assert.Throws<AggregateException>(() =>
                CreatePreview(
                    source,
                    () => view,
                    createPreview: (_, _, _) => throw creationFailure));

            Assert.Equal(
                new Exception[] { creationFailure, viewFailure, sourceFailure },
                exception.InnerExceptions);
        });
    }

    private static HomeHudPreview CreatePreview(
        TrackingSource source,
        Func<FrameworkElement> createView,
        Action<FrameworkElement, object>? assignDataContext = null,
        Func<IReadOnlyList<HomeWidgetConfig>?, double, double, HudSize>? calculateLayout = null,
        Func<FrameworkElement, HudSize, IDisposable, HomeHudPreview>? createPreview = null)
    {
        return HomeHudPreviewFactory.Create(
            source,
            createView,
            assignDataContext ?? ((view, dataContext) => view.DataContext = dataContext),
            calculateLayout ?? ((_, _, _) => new HudSize(100, 100)),
            createPreview ?? ((view, size, owner) =>
                new HomeHudPreview(view, size, owner)));
    }

    private sealed class TrackingSource : IHomeHudPresentationSource
    {
        public HomeHudDesignVariant DesignVariant => HomeHudDesignVariant.FusionBalanced;
        public IReadOnlyList<HomeWidgetConfig> ActiveWidgets => [];
        public double MaxWidgetWidth => 800;
        public double MaxWidgetHeight => 300;
        public object ViewDataContext => this;
        public Dispatcher? OwningDispatcher => null;
        public int InitializeCount { get; private set; }
        public int StartCount { get; private set; }
        public int DisposeCount { get; private set; }
        public Exception? DisposeException { get; init; }
        public event EventHandler? PresentationInvalidated
        {
            add { }
            remove { }
        }

        public void Initialize() => InitializeCount++;
        public void Start() => StartCount++;
        public void Stop() { }

        public void Dispose()
        {
            DisposeCount++;
            if (DisposeException is not null)
            {
                throw DisposeException;
            }
        }
    }

    private sealed class TrackingView : FrameworkElement, IDisposable
    {
        public int DisposeCount { get; private set; }
        public Exception? DisposeException { get; init; }

        public void Dispose()
        {
            DisposeCount++;
            if (DisposeException is not null)
            {
                throw DisposeException;
            }
        }
    }
}
