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
    public void GetView_ReusesSingleDynamicViewWhenLegacyDesignChanges()
    {
        StaTestRunner.Run(() =>
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
            Assert.Same(first, third);
            Assert.Equal([HomeHudDesignVariant.FusionBalanced], created);
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
    public async Task DisposeAsync_WhenCleanupCompletesSynchronously_FinalCleanupIsAlreadyComplete()
    {
        var source = new FakeHomeHudPresentationSource();
        var module = new HomeHudModule(source, _ => new FrameworkElement());

        await module.DisposeAsync();
        Task firstWait = module.WaitForFinalCleanupAsync(CancellationToken.None);
        Task secondWait = module.WaitForFinalCleanupAsync(CancellationToken.None);

        Assert.Same(firstWait, secondWait);
        Assert.True(firstWait.IsCompletedSuccessfully);
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
        StaTestRunner.Run(() =>
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
            if (!allowCreation.Wait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("View creation was not released in time.");
            }
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
        })
        {
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        try
        {
            Assert.True(creationStarted.Wait(TimeSpan.FromSeconds(5)));
            await module.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            allowCreation.Set();
            thread.Join(TimeSpan.FromSeconds(5));
        }

        Assert.False(thread.IsAlive);

        Assert.IsType<ObjectDisposedException>(getViewException);
        Assert.Equal(1, Assert.IsType<DisposableFrameworkElement>(view).DisposeCount);
        Assert.Throws<ObjectDisposedException>(
            () => module.GetView(new HudViewContext(HudPresentationState.Expanded)));
    }

    [Fact]
    public void GetView_WhenCreationLosesCacheRace_DisposesUnusedView()
    {
        StaTestRunner.Run(() =>
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
    public void DisposeAsync_ReleasesManagedOnlyCachedView()
    {
        StaTestRunner.Run(() =>
        {
            var source = new FakeHomeHudPresentationSource();
            var view = new ManagedOnlyView();
            var module = new HomeHudModule(source, _ => view);
            module.GetView(new HudViewContext(HudPresentationState.Expanded));

            module.DisposeAsync().AsTask().GetAwaiter().GetResult();

            Assert.Equal(1, view.ManagedReleaseCount);
        });
    }

    [Fact]
    public void DisposeAsync_WhenViewDisposeSucceeds_DoesNotDuplicateManagedRelease()
    {
        StaTestRunner.Run(() =>
        {
            var view = new ManagedCleanupView();
            var module = new HomeHudModule(
                new FakeHomeHudPresentationSource(),
                _ => view);
            module.GetView(new HudViewContext(HudPresentationState.Expanded));

            module.DisposeAsync().AsTask().GetAwaiter().GetResult();

            Assert.Equal(1, view.DisposeCount);
            Assert.Equal(0, view.ManagedReleaseCount);
        });
    }

    [Fact]
    public void DisposeAsync_WhenViewDisposeFails_AttemptsManagedRelease()
    {
        StaTestRunner.Run(() =>
        {
            var disposeFailure = new InvalidOperationException("dispose");
            var view = new ManagedCleanupView
            {
                DisposeException = disposeFailure
            };
            var module = new HomeHudModule(
                new FakeHomeHudPresentationSource(),
                _ => view);
            module.GetView(new HudViewContext(HudPresentationState.Expanded));

            Exception exception = Assert.Throws<InvalidOperationException>(
                () => module.DisposeAsync().AsTask().GetAwaiter().GetResult());

            Assert.Same(disposeFailure, exception);
            Assert.Equal(1, view.DisposeCount);
            Assert.Equal(1, view.ManagedReleaseCount);
        });
    }

    [Fact]
    public void DisposeAsync_WhenViewDisposeAndManagedReleaseFail_AggregatesBothFailures()
    {
        StaTestRunner.Run(() =>
        {
            var disposeFailure = new InvalidOperationException("dispose");
            var releaseFailure = new InvalidOperationException("release");
            var view = new ManagedCleanupView
            {
                DisposeException = disposeFailure,
                ManagedReleaseException = releaseFailure
            };
            var module = new HomeHudModule(
                new FakeHomeHudPresentationSource(),
                _ => view);
            module.GetView(new HudViewContext(HudPresentationState.Expanded));

            AggregateException exception = Assert.Throws<AggregateException>(
                () => module.DisposeAsync().AsTask().GetAwaiter().GetResult());

            Assert.Equal(
                new Exception[] { disposeFailure, releaseFailure },
                exception.InnerExceptions);
            Assert.Equal(1, view.DisposeCount);
            Assert.Equal(1, view.ManagedReleaseCount);
        });
    }

    [Fact]
    public void DisposeAsync_WhenCleanupOperationsFail_AttemptsAllAndAggregatesFailures()
    {
        StaTestRunner.Run(() =>
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
            var module = new HomeHudModule(
                source,
                _ => throwingView);
            module.InitializeAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
            module.ActivateAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
            module.GetView(new HudViewContext(HudPresentationState.Expanded));
            AggregateException exception = Assert.Throws<AggregateException>(
                () => module.DisposeAsync().AsTask().GetAwaiter().GetResult());

            Assert.Equal(4, exception.InnerExceptions.Count);
            Assert.Equal(1, throwingView.DisposeCount);
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
            try
            {
                Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
                var view = new DispatcherAwareView();
                var module = new HomeHudModule(source, _ => view);
                module.GetView(new HudViewContext(HudPresentationState.Expanded));
                ready.SetResult((module, dispatcher, view));
                Dispatcher.Run();
            }
            catch (Exception exception)
            {
                ready.TrySetException(exception);
            }
        })
        {
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        (HomeHudModule Module, Dispatcher Dispatcher, DispatcherAwareView View)? state = null;
        try
        {
            state = await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await state.Value.Module.DisposeAsync().AsTask()
                .WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(thread.ManagedThreadId, state.Value.View.DisposeThreadId);
        }
        finally
        {
            state?.Dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            thread.Join(TimeSpan.FromSeconds(5));
        }

        Assert.False(thread.IsAlive);
    }

    [Fact]
    public async Task DisposeAsync_DisposesSourceOnOwningDispatcher()
    {
        var ready = new TaskCompletionSource<(HomeHudModule Module, Dispatcher Dispatcher, FakeHomeHudPresentationSource Source)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
                var source = new FakeHomeHudPresentationSource
                {
                    OwningDispatcher = dispatcher
                };
                var module = new HomeHudModule(source, _ => new FrameworkElement());
                ready.SetResult((module, dispatcher, source));
                Dispatcher.Run();
            }
            catch (Exception exception)
            {
                ready.TrySetException(exception);
            }
        })
        {
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        (HomeHudModule Module, Dispatcher Dispatcher, FakeHomeHudPresentationSource Source)? state = null;
        try
        {
            state = await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await state.Value.Module.DisposeAsync().AsTask()
                .WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(thread.ManagedThreadId, state.Value.Source.DisposeThreadId);
        }
        finally
        {
            state?.Dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            thread.Join(TimeSpan.FromSeconds(5));
        }

        Assert.False(thread.IsAlive);
    }
    [Fact]
    public async Task GetView_WhenFactoryReturnsForeignDispatcherView_DisposesItOnOwner()
    {
        var ready = new TaskCompletionSource<(Dispatcher Dispatcher, DispatcherAwareView View)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            ready.SetResult((Dispatcher.CurrentDispatcher, new DispatcherAwareView()));
            Dispatcher.Run();
        })
        {
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        try
        {
            var state = await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var module = new HomeHudModule(
                new FakeHomeHudPresentationSource(),
                _ => state.View);

            StaTestRunner.Run(() => Assert.Throws<InvalidOperationException>(() =>
                module.GetView(new HudViewContext(HudPresentationState.Expanded))));

            Assert.Equal(thread.ManagedThreadId, state.View.DisposeThreadId);
            await module.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            if (ready.Task.IsCompletedSuccessfully)
            {
                (await ready.Task).Dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            }

            thread.Join(TimeSpan.FromSeconds(5));
        }

        Assert.False(thread.IsAlive);
    }

    [Fact]
    public async Task GetView_WhenRejectedViewIsManagedOnly_ReleasesManagedResources()
    {
        await RunWithForeignDispatcherViewAsync(
            () => new ManagedOnlyView(),
            view =>
            {
                var module = new HomeHudModule(
                    new FakeHomeHudPresentationSource(),
                    _ => view);

                StaTestRunner.Run(() => Assert.Throws<InvalidOperationException>(() =>
                    module.GetView(new HudViewContext(HudPresentationState.Expanded))));

                Assert.Equal(1, view.ManagedReleaseCount);
                return module.DisposeAsync().AsTask();
            });
    }

    [Fact]
    public async Task GetView_WhenRejectedViewDisposeSucceeds_DoesNotDuplicateManagedRelease()
    {
        await RunWithForeignDispatcherViewAsync(
            () => new ManagedCleanupView(),
            view =>
            {
                var module = new HomeHudModule(
                    new FakeHomeHudPresentationSource(),
                    _ => view);

                StaTestRunner.Run(() => Assert.Throws<InvalidOperationException>(() =>
                    module.GetView(new HudViewContext(HudPresentationState.Expanded))));

                Assert.Equal(1, view.DisposeCount);
                Assert.Equal(0, view.ManagedReleaseCount);
                return module.DisposeAsync().AsTask();
            });
    }

    [Fact]
    public async Task GetView_WhenRejectedViewDisposeAndManagedReleaseFail_PreservesAllFailures()
    {
        var disposeFailure = new InvalidOperationException("dispose");
        var releaseFailure = new InvalidOperationException("release");
        await RunWithForeignDispatcherViewAsync(
            () => new ManagedCleanupView
            {
                DisposeException = disposeFailure,
                ManagedReleaseException = releaseFailure
            },
            view =>
            {
                var module = new HomeHudModule(
                    new FakeHomeHudPresentationSource(),
                    _ => view);

                StaTestRunner.Run(() =>
                {
                    AggregateException exception = Assert.Throws<AggregateException>(() =>
                        module.GetView(new HudViewContext(HudPresentationState.Expanded)));

                    Assert.IsType<InvalidOperationException>(exception.InnerExceptions[0]);
                    Assert.Same(disposeFailure, exception.InnerExceptions[1]);
                    Assert.Same(releaseFailure, exception.InnerExceptions[2]);
                });
                return module.DisposeAsync().AsTask();
            });
    }

    [Fact]
    public async Task DisposeAsync_WhenDispatcherShutdownStarted_ReleasesManagedReferencesAndReportsFailure()
    {
        var ready = new TaskCompletionSource<(HomeHudModule Module, ManagedCleanupView View)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new FakeHomeHudPresentationSource();
        var thread = new Thread(() =>
        {
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            var view = new ManagedCleanupView();
            var module = new HomeHudModule(source, _ => view);
            module.GetView(new HudViewContext(HudPresentationState.Expanded));
            dispatcher.InvokeShutdown();
            ready.SetResult((module, view));
        })
        {
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        try
        {
            var state = await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));

            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await state.Module.DisposeAsync().AsTask()
                    .WaitAsync(TimeSpan.FromSeconds(5)));

            Assert.Contains("Dispatcher", exception.Message, StringComparison.Ordinal);
            Assert.Equal(1, state.View.ManagedReleaseCount);
            Assert.Equal(0, state.View.DisposeCount);
            Assert.Equal(1, source.DisposeCount);
            Assert.True(
                state.Module.WaitForFinalCleanupAsync(CancellationToken.None)
                    .IsCompletedSuccessfully);
        }
        finally
        {
            thread.Join(TimeSpan.FromSeconds(5));
        }

        Assert.False(thread.IsAlive);
    }

    [Fact]
    public async Task DisposeAsync_WhenDispatcherIsUnresponsive_TimesOutAndReleasesManagedReferences()
    {
        using var releaseThread = new ManualResetEventSlim();
        var ready = new TaskCompletionSource<(HomeHudModule Module, ManagedCleanupView View)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new FakeHomeHudPresentationSource();
        var thread = new Thread(() =>
        {
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            var view = new ManagedCleanupView();
            var module = new HomeHudModule(
                source,
                _ => view,
                TimeSpan.FromMilliseconds(100));
            module.GetView(new HudViewContext(HudPresentationState.Expanded));
            ready.SetResult((module, view));
            releaseThread.Wait(TimeSpan.FromSeconds(5));
            dispatcher.InvokeShutdown();
        })
        {
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        try
        {
            var state = await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));

            await Assert.ThrowsAsync<TimeoutException>(async () =>
                await state.Module.DisposeAsync().AsTask()
                    .WaitAsync(TimeSpan.FromSeconds(2)));

            Assert.Equal(1, state.View.ManagedReleaseCount);
            Assert.Equal(0, state.View.DisposeCount);
            Assert.Equal(1, source.DisposeCount);
        }
        finally
        {
            releaseThread.Set();
            thread.Join(TimeSpan.FromSeconds(5));
        }

        Assert.False(thread.IsAlive);
    }

    [Fact]
    public async Task DisposeAsync_WhenDispatcherDisposalIsExecuting_TimesOutWithoutConcurrentFallback()
    {
        using var allowDisposeToFinish = new ManualResetEventSlim();
        var disposeStarted = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var disposeFinished = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var lateFailureReported = new TaskCompletionSource<Exception>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int lateFailureReportCount = 0;
        var ready = new TaskCompletionSource<(HomeHudModule Module, BlockingManagedView View)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new FakeHomeHudPresentationSource();
        var disposeFailure = new InvalidOperationException("late dispose");
        var thread = new Thread(() =>
        {
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            var view = new BlockingManagedView(
                disposeStarted,
                disposeFinished,
                allowDisposeToFinish,
                disposeFailure);
            var module = new HomeHudModule(
                source,
                _ => view,
                TimeSpan.FromMilliseconds(100),
                exception =>
                {
                    Interlocked.Increment(ref lateFailureReportCount);
                    lateFailureReported.TrySetResult(exception);
                });
            module.GetView(new HudViewContext(HudPresentationState.Expanded));
            ready.SetResult((module, view));
            Dispatcher.Run();
        })
        {
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        (HomeHudModule Module, BlockingManagedView View)? state = null;
        try
        {
            state = await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Task disposal = state.Value.Module.DisposeAsync().AsTask();
            await disposeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            await Assert.ThrowsAsync<TimeoutException>(
                async () => await disposal.WaitAsync(TimeSpan.FromSeconds(2)));

            Assert.Equal(0, state.Value.View.ManagedReleaseCount);
            Assert.Equal(0, source.DisposeCount);
            Task firstFinalCleanup =
                state.Value.Module.WaitForFinalCleanupAsync(CancellationToken.None);
            Task secondFinalCleanup =
                state.Value.Module.WaitForFinalCleanupAsync(CancellationToken.None);
            Assert.Same(firstFinalCleanup, secondFinalCleanup);
            Assert.False(firstFinalCleanup.IsCompleted);

            allowDisposeToFinish.Set();
            await disposeFinished.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Exception lateFailure =
                await lateFailureReported.Task.WaitAsync(TimeSpan.FromSeconds(5));
            AggregateException finalCleanupFailure =
                await Assert.ThrowsAsync<AggregateException>(
                    async () => await firstFinalCleanup.WaitAsync(TimeSpan.FromSeconds(5)));

            Assert.Contains(
                disposeFailure,
                Assert.IsType<AggregateException>(lateFailure).InnerExceptions);
            Assert.Contains(disposeFailure, finalCleanupFailure.InnerExceptions);
            Assert.Equal(1, state.Value.View.DisposeCount);
            Assert.Equal(1, state.Value.View.ManagedReleaseCount);
            Assert.Equal(1, source.DisposeCount);
            Assert.Equal(1, Volatile.Read(ref lateFailureReportCount));
        }
        finally
        {
            allowDisposeToFinish.Set();
            if (state is not null)
            {
                state.Value.View.Dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            }

            thread.Join(TimeSpan.FromSeconds(5));
        }

        Assert.False(thread.IsAlive);
    }

    [Fact]
    public async Task GetView_WhenForeignViewCleanupFails_PreservesRejectionFailure()
    {
        var ready = new TaskCompletionSource<(Dispatcher Dispatcher, ManagedCleanupView View)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var view = new ManagedCleanupView
            {
                DisposeException = new InvalidOperationException("dispose")
            };
            ready.SetResult((Dispatcher.CurrentDispatcher, view));
            Dispatcher.Run();
        })
        {
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        try
        {
            var state = await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var module = new HomeHudModule(
                new FakeHomeHudPresentationSource(),
                _ => state.View);

            StaTestRunner.Run(() =>
            {
                AggregateException exception = Assert.Throws<AggregateException>(() =>
                    module.GetView(new HudViewContext(HudPresentationState.Expanded)));

                Assert.IsType<InvalidOperationException>(exception.InnerExceptions[0]);
                Assert.Same(state.View.DisposeException, exception.InnerExceptions[1]);
                Assert.Equal(1, state.View.ManagedReleaseCount);
            });
            await module.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            if (ready.Task.IsCompletedSuccessfully)
            {
                (await ready.Task).Dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            }

            thread.Join(TimeSpan.FromSeconds(5));
        }

        Assert.False(thread.IsAlive);
    }

    [Fact]
    public async Task WaitForFinalCleanupAsync_WhenDispatcherCleanupRemainsBlocked_CanBeCanceledAndRetried()
    {
        using var allowDisposeToFinish = new ManualResetEventSlim();
        var disposeStarted = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var disposeFinished = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var ready = new TaskCompletionSource<(HomeHudModule Module, Dispatcher Dispatcher)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new FakeHomeHudPresentationSource();
        var thread = new Thread(() =>
        {
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            var view = new BlockingManagedView(
                disposeStarted,
                disposeFinished,
                allowDisposeToFinish,
                disposeException: null);
            var module = new HomeHudModule(
                source,
                _ => view,
                TimeSpan.FromMilliseconds(100));
            module.GetView(new HudViewContext(HudPresentationState.Expanded));
            ready.SetResult((module, dispatcher));
            Dispatcher.Run();
        })
        {
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        (HomeHudModule Module, Dispatcher Dispatcher)? state = null;
        try
        {
            state = await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Task disposal = state.Value.Module.DisposeAsync().AsTask();
            await disposeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAsync<TimeoutException>(
                async () => await disposal.WaitAsync(TimeSpan.FromSeconds(2)));

            using var cancellation = new CancellationTokenSource(
                TimeSpan.FromMilliseconds(100));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await state.Value.Module.WaitForFinalCleanupAsync(
                    cancellation.Token));
            Assert.Equal(0, source.DisposeCount);

            allowDisposeToFinish.Set();
            await state.Value.Module.WaitForFinalCleanupAsync(CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, source.DisposeCount);
        }
        finally
        {
            allowDisposeToFinish.Set();
            state?.Dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            thread.Join(TimeSpan.FromSeconds(5));
        }

        Assert.False(thread.IsAlive);
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

    [Fact]
    public async Task RunWithForeignDispatcherViewAsync_WhenViewCreationFails_PreservesFailure()
    {
        var failure = new InvalidOperationException("create");

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RunWithForeignDispatcherViewAsync<FrameworkElement>(() => throw failure, _ => Task.CompletedTask));

        Assert.Same(failure, exception);
    }

    private static async Task RunWithForeignDispatcherViewAsync<TView>(
        Func<TView> createView,
        Func<TView, Task> runScenario)
        where TView : FrameworkElement
    {
        var ready = new TaskCompletionSource<(Dispatcher Dispatcher, TView View)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                ready.SetResult((Dispatcher.CurrentDispatcher, createView()));
                Dispatcher.Run();
            }
            catch (Exception exception)
            {
                ready.TrySetException(exception);
            }
        })
        {
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        try
        {
            var state = await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await runScenario(state.View).WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            if (ready.Task.IsCompletedSuccessfully)
            {
                (await ready.Task).Dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            }

            thread.Join(TimeSpan.FromSeconds(5));
        }

        Assert.False(thread.IsAlive);
    }

    private sealed class FakeHomeHudPresentationSource : IHomeHudPresentationSource
    {
        private EventHandler? _presentationInvalidated;

        public HomeHudDesignVariant DesignVariant { get; set; } =
            HomeHudDesignVariant.FusionBalanced;

        public IReadOnlyList<NoraBar.Hud.Home.Widgets.HomeWidgetConfig> ActiveWidgets { get; set; } = [];

        public double MaxWidgetWidth { get; set; } = 800;
        public double MaxWidgetHeight { get; set; } = 300;

        public object ViewDataContext { get; } = new();
        public Dispatcher? OwningDispatcher { get; init; }
        public int InitializeCount { get; private set; }
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public int DisposeCount { get; private set; }
        public int? DisposeThreadId { get; private set; }
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
            DisposeThreadId = Environment.CurrentManagedThreadId;
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

    private sealed class ManagedCleanupView : FrameworkElement, IDisposable, IHomeHudManagedResource
    {
        internal int DisposeCount { get; private set; }
        internal int ManagedReleaseCount { get; private set; }
        internal Exception? DisposeException { get; init; }
        internal Exception? ManagedReleaseException { get; init; }

        public void Dispose()
        {
            Assert.True(Dispatcher.CheckAccess());
            DisposeCount++;
            if (DisposeException is not null)
            {
                throw DisposeException;
            }
        }

        public void ReleaseManagedResources()
        {
            ManagedReleaseCount++;
            if (ManagedReleaseException is not null)
            {
                throw ManagedReleaseException;
            }
        }
    }

    private sealed class ManagedOnlyView : FrameworkElement, IHomeHudManagedResource
    {
        internal int ManagedReleaseCount { get; private set; }

        public void ReleaseManagedResources() => ManagedReleaseCount++;
    }

    private sealed class BlockingManagedView : FrameworkElement, IDisposable, IHomeHudManagedResource
    {
        private readonly TaskCompletionSource<object?> _disposeStarted;
        private readonly TaskCompletionSource<object?> _disposeFinished;
        private readonly ManualResetEventSlim _allowDisposeToFinish;
        private readonly Exception? _disposeException;
        private int _managedReleaseCount;

        internal BlockingManagedView(
            TaskCompletionSource<object?> disposeStarted,
            TaskCompletionSource<object?> disposeFinished,
            ManualResetEventSlim allowDisposeToFinish,
            Exception? disposeException)
        {
            _disposeStarted = disposeStarted;
            _disposeFinished = disposeFinished;
            _allowDisposeToFinish = allowDisposeToFinish;
            _disposeException = disposeException;
        }

        internal int DisposeCount { get; private set; }
        internal int ManagedReleaseCount => Volatile.Read(ref _managedReleaseCount);

        public void Dispose()
        {
            DisposeCount++;
            _disposeStarted.TrySetResult(null);
            try
            {
                if (!_allowDisposeToFinish.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new TimeoutException("Blocked view disposal was not released.");
                }

                if (_disposeException is not null)
                {
                    throw _disposeException;
                }
            }
            finally
            {
                _disposeFinished.TrySetResult(null);
            }
        }

        public void ReleaseManagedResources() =>
            Interlocked.Increment(ref _managedReleaseCount);
    }
}
