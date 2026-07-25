using System.Windows;
using System.Windows.Threading;
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

    [Fact]
    public void DisposeAsync_DisposesCachedViews()
    {
        RunInSta(() =>
        {
            var source = new FakeHomeHudPresentationSource();
            var view = new DisposableFrameworkElement();
            var module = new HomeHudModule(source, _ => view);
            module.GetView(new HudViewContext(HudPresentationState.Expanded));

            module.DisposeAsync().AsTask().GetAwaiter().GetResult();

            Assert.Equal(1, view.DisposeCount);
        });
    }

    [Fact]
    public async Task DisposeAsync_WhenViewCreationLosesRace_DisposesRejectedView()
    {
        using var creationStarted = new ManualResetEventSlim();
        using var allowCreation = new ManualResetEventSlim();
        var source = new FakeHomeHudPresentationSource();
        DisposableFrameworkElement? view = null;
        var module = new HomeHudModule(source, _ =>
        {
            view = new DisposableFrameworkElement();
            creationStarted.Set();
            allowCreation.Wait();
            return view;
        });
        Exception? getViewException = null;
        var thread = new Thread(() =>
        {
            try
            {
                module.GetView(new HudViewContext(HudPresentationState.Expanded));
            }
            catch (Exception exception)
            {
                getViewException = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(creationStarted.Wait(TimeSpan.FromSeconds(5)));

        await module.DisposeAsync();
        allowCreation.Set();
        Assert.True(thread.Join(TimeSpan.FromSeconds(5)));

        Assert.IsType<ObjectDisposedException>(getViewException);
        Assert.Equal(1, Assert.IsType<DisposableFrameworkElement>(view).DisposeCount);
        Assert.Throws<ObjectDisposedException>(
            () => module.GetView(new HudViewContext(HudPresentationState.Expanded)));
    }

    [Fact]
    public void GetView_WhenCreationLosesCacheRace_DisposesUnusedView()
    {
        RunInSta(() =>
        {
            var source = new FakeHomeHudPresentationSource();
            var unusedView = new DisposableFrameworkElement();
            var acceptedView = new DisposableFrameworkElement();
            HomeHudModule? module = null;
            int creationCount = 0;
            module = new HomeHudModule(source, _ =>
            {
                creationCount++;
                if (creationCount == 1)
                {
                    module!.GetView(new HudViewContext(HudPresentationState.Expanded));
                    return unusedView;
                }

                return acceptedView;
            });

            FrameworkElement result = module.GetView(
                new HudViewContext(HudPresentationState.Expanded));

            Assert.Same(acceptedView, result);
            Assert.Equal(1, unusedView.DisposeCount);
            Assert.Equal(0, acceptedView.DisposeCount);
            module.DisposeAsync().AsTask().GetAwaiter().GetResult();
            Assert.Equal(1, acceptedView.DisposeCount);
        });
    }

    [Fact]
    public void DisposeAsync_WhenCleanupOperationsFail_AttemptsAllAndAggregatesFailures()
    {
        RunInSta(() =>
        {
            var source = new FakeHomeHudPresentationSource
            {
                StopException = new InvalidOperationException("stop"),
                UnsubscribeException = new InvalidOperationException("unsubscribe"),
                DisposeException = new InvalidOperationException("source")
            };
            var throwingView = new DisposableFrameworkElement
            {
                DisposeException = new InvalidOperationException("view")
            };
            var otherView = new DisposableFrameworkElement();
            var module = new HomeHudModule(
                source,
                variant => variant == HomeHudDesignVariant.FusionBalanced
                    ? throwingView
                    : otherView);
            module.InitializeAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
            module.ActivateAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
            module.GetView(new HudViewContext(HudPresentationState.Expanded));
            source.DesignVariant = HomeHudDesignVariant.FusionExpressive;
            module.GetView(new HudViewContext(HudPresentationState.Expanded));

            AggregateException exception = Assert.Throws<AggregateException>(
                () => module.DisposeAsync().AsTask().GetAwaiter().GetResult());

            Assert.Equal(4, exception.InnerExceptions.Count);
            Assert.Equal(1, throwingView.DisposeCount);
            Assert.Equal(1, otherView.DisposeCount);
            Assert.Equal(1, source.DisposeCount);
            Assert.Throws<ObjectDisposedException>(
                () => module.GetPreferredSize(
                    new HudViewContext(HudPresentationState.Expanded)));
        });
    }

    [Fact]
    public async Task DisposeAsync_DisposesViewsOnOwningDispatcher()
    {
        var source = new FakeHomeHudPresentationSource();
        var ready = new TaskCompletionSource<(HomeHudModule Module, Dispatcher Dispatcher, DispatcherAwareView View)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            var view = new DispatcherAwareView();
            var module = new HomeHudModule(source, _ => view);
            module.GetView(new HudViewContext(HudPresentationState.Expanded));
            ready.SetResult((module, dispatcher, view));
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        var state = await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await state.Module.DisposeAsync();

        Assert.Equal(thread.ManagedThreadId, state.View.DisposeThreadId);
        state.Dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
        Assert.True(thread.Join(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task LifecycleMethods_AfterDisposalStarts_DoNotReuseModule()
    {
        var source = new FakeHomeHudPresentationSource();
        var module = new HomeHudModule(source, _ => new FrameworkElement());

        ValueTask dispose = module.DisposeAsync();
        await module.InitializeAsync(CancellationToken.None);
        await module.ActivateAsync(CancellationToken.None);
        await dispose;

        Assert.Equal(0, source.InitializeCount);
        Assert.Equal(0, source.StartCount);
        Assert.Throws<ObjectDisposedException>(
            () => module.GetPreferredSize(
                new HudViewContext(HudPresentationState.Expanded)));
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
        private EventHandler? _presentationInvalidated;

        public HomeHudDesignVariant DesignVariant { get; set; } =
            HomeHudDesignVariant.FusionBalanced;

        public IReadOnlyList<NoraBar.Hud.Home.Widgets.HomeWidgetConfig>? ActiveWidgets { get; set; }

        public double MaxWidgetWidth { get; set; } = 800;
        public double MaxWidgetHeight { get; set; } = 300;

        public object ViewDataContext { get; } = new();
        public int InitializeCount { get; private set; }
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public int DisposeCount { get; private set; }
        public Exception? StopException { get; init; }
        public Exception? UnsubscribeException { get; init; }
        public Exception? DisposeException { get; init; }

        public event EventHandler? PresentationInvalidated
        {
            add => _presentationInvalidated += value;
            remove
            {
                _presentationInvalidated -= value;
                if (UnsubscribeException is not null)
                {
                    throw UnsubscribeException;
                }
            }
        }

        public void Initialize() => InitializeCount++;

        public void Start() => StartCount++;

        public void Stop()
        {
            StopCount++;
            if (StopException is not null)
            {
                throw StopException;
            }
        }

        public void Dispose()
        {
            DisposeCount++;
            if (DisposeException is not null)
            {
                throw DisposeException;
            }
        }

        public void RaisePresentationInvalidated() =>
            _presentationInvalidated?.Invoke(this, EventArgs.Empty);
    }

    private sealed class DisposableFrameworkElement : FrameworkElement, IDisposable
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

    private sealed class DispatcherAwareView : FrameworkElement, IDisposable
    {
        internal int DisposeThreadId { get; private set; }

        public void Dispose()
        {
            Assert.True(Dispatcher.CheckAccess());
            DisposeThreadId = Environment.CurrentManagedThreadId;
        }
    }
}
