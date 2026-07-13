using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using NoraBar.Hud;
using NoraBar.Hud.Music;
using NoraBar.Models;
using NoraBar.ViewModels;
using Xunit;

namespace NoraBar.Tests.Hud;

public sealed class MusicHudModuleTests
{
    [Fact]
    public void Constructor_UsesBuiltInMusicIdentity()
    {
        var source = new FakeMusicHudPresentationSource();
        var module = CreateModule(source);

        Assert.Equal(BuiltInHudIds.Music, module.Id);
        Assert.Equal(new HudModuleMetadata("Music", 0), module.Metadata);
    }

    [Fact]
    public void GetView_SameVariantReturnsCachedViewAndRestoresDataContext()
    {
        RunInSta(() =>
        {
            var source = new FakeMusicHudPresentationSource();
            var createdViews = new List<FrameworkElement>();
            MusicHudModule module = CreateModule(source, _ =>
            {
                var view = new Border();
                createdViews.Add(view);
                return view;
            });

            FrameworkElement first = module.GetView(new HudViewContext(HudPresentationState.Collapsed));
            first.DataContext = null;
            FrameworkElement second = module.GetView(new HudViewContext(HudPresentationState.Expanded));

            Assert.Same(first, second);
            Assert.Single(createdViews);
            Assert.Same(source.ViewDataContext, second.DataContext);
        });
    }

    [Fact]
    public void GetView_EachDesignIsCreatedOnceAndUsesCompatibleDataContext()
    {
        RunInSta(() =>
        {
            var source = new FakeMusicHudPresentationSource();
            var creationCounts = Enum.GetValues<DesignVariant>().ToDictionary(variant => variant, _ => 0);
            MusicHudModule module = CreateModule(source, variant =>
            {
                creationCounts[variant]++;
                return new Border();
            });

            foreach (DesignVariant variant in Enum.GetValues<DesignVariant>())
            {
                source.CurrentVariant = variant;
                FrameworkElement first = module.GetView(new HudViewContext(HudPresentationState.Collapsed));
                FrameworkElement second = module.GetView(new HudViewContext(HudPresentationState.Expanded));

                Assert.Same(first, second);
                Assert.Same(source.ViewDataContext, first.DataContext);
            }

            Assert.All(creationCounts.Values, count => Assert.Equal(1, count));
        });
    }

    [Fact]
    public async Task InitializeAsync_CalledTwiceSubscribesOnce()
    {
        var source = new FakeMusicHudPresentationSource();
        MusicHudModule module = CreateModule(source);

        await module.InitializeAsync(CancellationToken.None);
        await module.InitializeAsync(CancellationToken.None);

        Assert.Equal(1, source.ShellSubscribeCount);
        Assert.Equal(1, source.MusicSubscribeCount);
    }

    [Theory]
    [InlineData(nameof(MainViewModel.CurrentVariant))]
    [InlineData(nameof(MainViewModel.ShowProgressBar))]
    [InlineData(nameof(MainViewModel.ShowLyrics))]
    public async Task ShellLayoutPropertyChanged_RaisesPresentationInvalidated(string propertyName)
    {
        var source = new FakeMusicHudPresentationSource();
        MusicHudModule module = CreateModule(source);
        int invalidationCount = 0;
        module.PresentationInvalidated += (_, _) => invalidationCount++;
        await module.InitializeAsync(CancellationToken.None);

        source.RaiseShellPropertyChanged(propertyName);

        Assert.Equal(1, invalidationCount);
    }

    [Fact]
    public async Task MusicSessionCountChanged_RaisesPresentationInvalidated()
    {
        var source = new FakeMusicHudPresentationSource();
        MusicHudModule module = CreateModule(source);
        int invalidationCount = 0;
        module.PresentationInvalidated += (_, _) => invalidationCount++;
        await module.InitializeAsync(CancellationToken.None);

        source.RaiseMusicPropertyChanged(nameof(MusicViewModel.HasMultipleSessions));

        Assert.Equal(1, invalidationCount);
    }

    [Fact]
    public async Task UnrelatedProperties_DoNotRaisePresentationInvalidated()
    {
        var source = new FakeMusicHudPresentationSource();
        MusicHudModule module = CreateModule(source);
        int invalidationCount = 0;
        module.PresentationInvalidated += (_, _) => invalidationCount++;
        await module.InitializeAsync(CancellationToken.None);

        source.RaiseShellPropertyChanged(nameof(MainViewModel.CurrentPage));
        source.RaiseShellPropertyChanged(nameof(MainViewModel.TextScrollMode));
        source.RaiseMusicPropertyChanged(nameof(MusicViewModel.Title));

        Assert.Equal(0, invalidationCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task AllPropertiesChanged_RaisesPresentationInvalidated(string? propertyName)
    {
        var source = new FakeMusicHudPresentationSource();
        MusicHudModule module = CreateModule(source);
        int invalidationCount = 0;
        module.PresentationInvalidated += (_, _) => invalidationCount++;
        await module.InitializeAsync(CancellationToken.None);

        source.RaiseShellPropertyChanged(propertyName);
        source.RaiseMusicPropertyChanged(propertyName);

        Assert.Equal(2, invalidationCount);
    }

    [Fact]
    public async Task InitializeAsync_WhenMusicSubscriptionFailsRollsBackBothSubscriptions()
    {
        var source = new FakeMusicHudPresentationSource
        {
            MusicSubscribeException = new InvalidOperationException("music add failed")
        };
        MusicHudModule module = CreateModule(source);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => module.InitializeAsync(CancellationToken.None).AsTask());

        Assert.Equal(1, source.ShellUnsubscribeCount);
        Assert.Equal(1, source.MusicUnsubscribeCount);
    }

    [Fact]
    public void GetView_FromDifferentDispatcherThrows()
    {
        var source = new FakeMusicHudPresentationSource();
        MusicHudModule module = CreateModule(source);
        RunInSta(() => module.GetView(new HudViewContext(HudPresentationState.Collapsed)));

        RunInSta(() => Assert.Throws<InvalidOperationException>(
            () => module.GetView(new HudViewContext(HudPresentationState.Expanded))));
    }

    [Fact]
    public void GetView_WhenInitialFactoryFailsAllowsRetryFromAnotherDispatcher()
    {
        var source = new FakeMusicHudPresentationSource();
        int attempts = 0;
        MusicHudModule module = CreateModule(source, _ =>
        {
            if (Interlocked.Increment(ref attempts) == 1)
            {
                throw new InvalidOperationException("factory failed");
            }

            return new Border();
        });
        RunInSta(() => Assert.Throws<InvalidOperationException>(
            () => module.GetView(new HudViewContext(HudPresentationState.Collapsed))));

        RunInSta(() => Assert.IsType<Border>(
            module.GetView(new HudViewContext(HudPresentationState.Collapsed))));

        Assert.Equal(2, attempts);
    }

    [Fact]
    public void GetPreferredSize_UsesCurrentPresentationValues()
    {
        var source = new FakeMusicHudPresentationSource
        {
            CurrentVariant = DesignVariant.ProductivityCommandIsland,
            ShowProgressBar = true,
            ShowLyrics = true,
            HasMultipleSessions = true
        };
        MusicHudModule module = CreateModule(source);

        HudSize result = module.GetPreferredSize(new HudViewContext(HudPresentationState.Expanded));

        Assert.Equal(new HudSize(560, 160), result);
    }

    [Fact]
    public async Task PresentationStateChanges_DoNotResubscribeOrCleanupPresentationSource()
    {
        var source = new FakeMusicHudPresentationSource();
        int viewCreationCount = 0;
        MusicHudModule module = CreateModule(source, _ =>
        {
            viewCreationCount++;
            return new Border();
        });
        await module.InitializeAsync(CancellationToken.None);
        await module.ActivateAsync(CancellationToken.None);
        await module.ActivateAsync(CancellationToken.None);

        RunInSta(() =>
        {
            module.GetView(new HudViewContext(HudPresentationState.Collapsed));
            module.GetView(new HudViewContext(HudPresentationState.Expanded));
        });
        await module.DeactivateAsync(CancellationToken.None);
        await module.DeactivateAsync(CancellationToken.None);

        Assert.Equal(1, source.ShellSubscribeCount);
        Assert.Equal(1, source.MusicSubscribeCount);
        Assert.Equal(0, source.CleanupCount);
        Assert.Equal(1, viewCreationCount);
    }

    [Fact]
    public async Task DisposeAsync_CalledTwiceUnsubscribesBeforeCleanupOnce()
    {
        var source = new FakeMusicHudPresentationSource();
        MusicHudModule module = CreateModule(source);
        await module.InitializeAsync(CancellationToken.None);

        await module.DisposeAsync();
        await module.DisposeAsync();
        source.RaiseShellPropertyChanged(nameof(MainViewModel.CurrentVariant));

        Assert.Equal(1, source.ShellUnsubscribeCount);
        Assert.Equal(1, source.MusicUnsubscribeCount);
        Assert.Equal(1, source.CleanupCount);
        Assert.Equal(["shell:add", "music:add", "shell:remove", "music:remove", "cleanup"], source.Calls);
    }

    [Fact]
    public async Task DisposeAsync_BeforeInitializeStillCleansUpOnce()
    {
        var source = new FakeMusicHudPresentationSource();
        MusicHudModule module = CreateModule(source);

        await module.DisposeAsync();
        await module.DisposeAsync();

        Assert.Equal(0, source.ShellSubscribeCount);
        Assert.Equal(0, source.MusicSubscribeCount);
        Assert.Equal(1, source.CleanupCount);
    }

    [Fact]
    public async Task DisposeAsync_WaitsForInFlightPresentationNotificationBeforeCleanup()
    {
        var source = new FakeMusicHudPresentationSource();
        MusicHudModule module = CreateModule(source);
        Task? dispose = null;
        bool? disposeCompletedInsideNotification = null;
        module.PresentationInvalidated += (_, _) =>
        {
            dispose = module.DisposeAsync().AsTask();
            disposeCompletedInsideNotification = dispose.IsCompleted;
        };
        await module.InitializeAsync(CancellationToken.None);

        source.RaiseShellPropertyChanged(nameof(MainViewModel.CurrentVariant));

        Assert.False(disposeCompletedInsideNotification);
        await Assert.IsAssignableFrom<Task>(dispose);
        Assert.Equal(1, source.CleanupCount);
    }

    [Fact]
    public async Task DisposeAsync_WhenCleanupStepsFailAttemptsEveryStepAndAggregatesErrors()
    {
        var source = new FakeMusicHudPresentationSource
        {
            ShellUnsubscribeException = new InvalidOperationException("shell remove failed"),
            MusicUnsubscribeException = new InvalidOperationException("music remove failed"),
            CleanupException = new InvalidOperationException("cleanup failed")
        };
        MusicHudModule module = CreateModule(source);
        await module.InitializeAsync(CancellationToken.None);

        AggregateException exception = await Assert.ThrowsAsync<AggregateException>(
            () => module.DisposeAsync().AsTask());

        Assert.Equal(3, exception.InnerExceptions.Count);
        Assert.Equal(1, source.ShellUnsubscribeCount);
        Assert.Equal(1, source.MusicUnsubscribeCount);
        Assert.Equal(1, source.CleanupCount);
    }

    [Fact]
    public async Task DisposeAsync_WhenUnsubscribeFailsBeforeRemovalRetriesWithoutRepeatingCleanup()
    {
        var source = new FakeMusicHudPresentationSource
        {
            ShellUnsubscribeFailuresBeforeRemove = 1,
            MusicUnsubscribeFailuresBeforeRemove = 1
        };
        MusicHudModule module = CreateModule(source);
        await module.InitializeAsync(CancellationToken.None);

        AggregateException exception = await Assert.ThrowsAsync<AggregateException>(
            () => module.DisposeAsync().AsTask());
        Assert.Equal(2, exception.InnerExceptions.Count);

        await module.DisposeAsync();

        Assert.Equal(2, source.ShellUnsubscribeCount);
        Assert.Equal(2, source.MusicUnsubscribeCount);
        Assert.Equal(0, source.ShellHandlerCount);
        Assert.Equal(0, source.MusicHandlerCount);
        Assert.Equal(1, source.CleanupCount);
    }

    [Fact]
    public async Task InitializeAsync_WhenRollbackRemovalFailsDisposeRetriesRemainingSubscription()
    {
        var source = new FakeMusicHudPresentationSource
        {
            MusicSubscribeException = new InvalidOperationException("music add failed"),
            MusicUnsubscribeFailuresBeforeRemove = 1
        };
        MusicHudModule module = CreateModule(source);

        await Assert.ThrowsAsync<AggregateException>(
            () => module.InitializeAsync(CancellationToken.None).AsTask());
        await module.DisposeAsync();

        Assert.Equal(1, source.ShellUnsubscribeCount);
        Assert.Equal(2, source.MusicUnsubscribeCount);
        Assert.Equal(0, source.ShellHandlerCount);
        Assert.Equal(0, source.MusicHandlerCount);
        Assert.Equal(1, source.CleanupCount);
    }

    [Fact]
    public async Task InitializeAsync_AfterRollbackRemovalFailureRemovesResidualBeforeResubscribing()
    {
        var source = new FakeMusicHudPresentationSource
        {
            MusicSubscribeException = new InvalidOperationException("music add failed"),
            MusicUnsubscribeFailuresBeforeRemove = 1
        };
        MusicHudModule module = CreateModule(source);
        await Assert.ThrowsAsync<AggregateException>(
            () => module.InitializeAsync(CancellationToken.None).AsTask());
        source.MusicSubscribeException = null;

        await module.InitializeAsync(CancellationToken.None);
        await module.DisposeAsync();

        Assert.Equal(2, source.ShellSubscribeCount);
        Assert.Equal(2, source.MusicSubscribeCount);
        Assert.Equal(2, source.ShellUnsubscribeCount);
        Assert.Equal(3, source.MusicUnsubscribeCount);
        Assert.Equal(0, source.ShellHandlerCount);
        Assert.Equal(0, source.MusicHandlerCount);
        Assert.Equal(1, source.CleanupCount);
    }

    private static MusicHudModule CreateModule(
        FakeMusicHudPresentationSource source,
        Func<DesignVariant, FrameworkElement>? createView = null)
    {
        createView ??= _ => new Border();
        var factories = Enum.GetValues<DesignVariant>().ToDictionary(
            variant => variant,
            variant => new Func<FrameworkElement>(() => createView(variant)));
        return new MusicHudModule(source, factories);
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
            catch (Exception caughtException)
            {
                exception = caughtException;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    private sealed class FakeMusicHudPresentationSource : IMusicHudPresentationSource
    {
        private PropertyChangedEventHandler? _shellPropertyChanged;
        private PropertyChangedEventHandler? _musicPropertyChanged;

        public DesignVariant CurrentVariant { get; set; } = DesignVariant.MinimalFloatingPill;

        public bool ShowProgressBar { get; set; }

        public bool ShowLyrics { get; set; }

        public bool HasMultipleSessions { get; set; }

        public object ViewDataContext { get; } = new object();

        public int ShellSubscribeCount { get; private set; }

        public int ShellUnsubscribeCount { get; private set; }

        public int MusicSubscribeCount { get; private set; }

        public int MusicUnsubscribeCount { get; private set; }

        public int CleanupCount { get; private set; }

        public Exception? MusicSubscribeException { get; set; }

        public Exception? ShellUnsubscribeException { get; init; }

        public Exception? MusicUnsubscribeException { get; init; }

        public Exception? CleanupException { get; init; }

        public int ShellUnsubscribeFailuresBeforeRemove { get; set; }

        public int MusicUnsubscribeFailuresBeforeRemove { get; set; }

        public int ShellHandlerCount => _shellPropertyChanged?.GetInvocationList().Length ?? 0;

        public int MusicHandlerCount => _musicPropertyChanged?.GetInvocationList().Length ?? 0;

        public List<string> Calls { get; } = [];

        public event PropertyChangedEventHandler? ShellPropertyChanged
        {
            add
            {
                ShellSubscribeCount++;
                Calls.Add("shell:add");
                _shellPropertyChanged += value;
            }
            remove
            {
                ShellUnsubscribeCount++;
                Calls.Add("shell:remove");
                if (ShellUnsubscribeFailuresBeforeRemove > 0)
                {
                    ShellUnsubscribeFailuresBeforeRemove--;
                    throw new InvalidOperationException("shell remove failed before removal");
                }

                _shellPropertyChanged -= value;
                if (ShellUnsubscribeException is not null)
                {
                    throw ShellUnsubscribeException;
                }
            }
        }

        public event PropertyChangedEventHandler? MusicPropertyChanged
        {
            add
            {
                MusicSubscribeCount++;
                Calls.Add("music:add");
                _musicPropertyChanged += value;
                if (MusicSubscribeException is not null)
                {
                    throw MusicSubscribeException;
                }
            }
            remove
            {
                MusicUnsubscribeCount++;
                Calls.Add("music:remove");
                if (MusicUnsubscribeFailuresBeforeRemove > 0)
                {
                    MusicUnsubscribeFailuresBeforeRemove--;
                    throw new InvalidOperationException("music remove failed before removal");
                }

                _musicPropertyChanged -= value;
                if (MusicUnsubscribeException is not null)
                {
                    throw MusicUnsubscribeException;
                }
            }
        }

        public void Cleanup()
        {
            CleanupCount++;
            Calls.Add("cleanup");
            if (CleanupException is not null)
            {
                throw CleanupException;
            }
        }

        public void RaiseShellPropertyChanged(string? propertyName)
        {
            _shellPropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void RaiseMusicPropertyChanged(string? propertyName)
        {
            _musicPropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
