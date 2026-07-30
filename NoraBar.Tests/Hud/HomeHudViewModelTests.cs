using System.ComponentModel;
using System.Reflection;
using System.Windows.Threading;
using NoraBar.Hud.Home;
using NoraBar.ViewModels;
using Xunit;

namespace NoraBar.Tests.Hud;

public sealed class HomeHudViewModelTests
{
    [Fact]
    public void WorldClockItems_RepeatedClockRefreshesReuseCollectionWithoutReplacementNotification()
    {
        StaTestRunner.Run(() =>
        {
            var source = new TestMainViewModel();
            DateTimeOffset now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
            var viewModel = new HomeHudViewModel(
                source,
                new DispatcherTimer(DispatcherPriority.Background),
                () => now);
            var changedProperties = new List<string?>();
            viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

            try
            {
                viewModel.Initialize();
                IReadOnlyList<HomeWorldClockItemViewModel> boundCollection = viewModel.WorldClockItems;
                MethodInfo refreshClock = typeof(HomeHudViewModel).GetMethod(
                    "RefreshClock",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("RefreshClock was not found.");
                changedProperties.Clear();

                now = now.AddSeconds(1);
                refreshClock.Invoke(viewModel, null);
                now = now.AddSeconds(1);
                refreshClock.Invoke(viewModel, null);

                Assert.Same(boundCollection, viewModel.WorldClockItems);
                Assert.DoesNotContain(nameof(HomeHudViewModel.WorldClockItems), changedProperties);
            }
            finally
            {
                viewModel.Dispose();
                source.Music.Cleanup();
            }
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AllPropertiesChanged_RefreshesPresentationState(string? propertyName)
    {
        StaTestRunner.Run(() =>
        {
            var source = new TestMainViewModel();
            DateTimeOffset now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
            var viewModel = new HomeHudViewModel(
                source,
                new DispatcherTimer(DispatcherPriority.Background),
                () => now);
            var changedProperties = new List<string?>();
            int invalidationCount = 0;
            viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);
            viewModel.PresentationInvalidated += (_, _) => invalidationCount++;

            try
            {
                viewModel.Initialize();
                changedProperties.Clear();
                now = now.AddDays(1).AddMinutes(1);

                source.RaisePropertyChanged(propertyName);

                Assert.Contains(nameof(HomeHudViewModel.LocalTimeText), changedProperties);
                Assert.Contains(nameof(HomeHudViewModel.LocalDateText), changedProperties);
                Assert.Contains(nameof(HomeHudViewModel.FirstWorldClockTimeText), changedProperties);
                Assert.Contains(nameof(HomeHudViewModel.SecondWorldClockTimeText), changedProperties);
                Assert.Contains(nameof(HomeHudViewModel.ActiveWidgets), changedProperties);
                Assert.Contains(nameof(HomeHudViewModel.MaxWidgetWidth), changedProperties);
                Assert.Contains(nameof(HomeHudViewModel.MaxWidgetHeight), changedProperties);
                Assert.Contains(nameof(HomeHudViewModel.IsWidgetEditMode), changedProperties);
                Assert.Contains(nameof(HomeHudViewModel.MediaTitle), changedProperties);
                Assert.Contains(nameof(HomeHudViewModel.FirstWorldClockLabel), changedProperties);
                Assert.Contains(nameof(HomeHudViewModel.SecondWorldClockLabel), changedProperties);
                Assert.DoesNotContain(nameof(HomeHudViewModel.WorldClockItems), changedProperties);
                Assert.Equal(1, invalidationCount);
            }
            finally
            {
                viewModel.Dispose();
                source.Music.Cleanup();
            }
        });
    }

    [Fact]
    public void WorldClockItems_UpdatesWithMainViewModelEntriesAndReordering()
    {
        StaTestRunner.Run(() =>
        {
            var source = new TestMainViewModel();
            DateTimeOffset now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
            var viewModel = new HomeHudViewModel(
                source,
                new DispatcherTimer(DispatcherPriority.Background),
                () => now);

            try
            {
                viewModel.Initialize();
                IReadOnlyList<HomeWorldClockItemViewModel> boundCollection = viewModel.WorldClockItems;

                // Setup deterministic 3 entries
                while (source.WorldClockEntries.Count < 3)
                {
                    source.AddWorldClockCommand.Execute(null);
                }
                while (source.WorldClockEntries.Count > 3)
                {
                    source.RemoveWorldClockCommand.Execute(source.WorldClockEntries[source.WorldClockEntries.Count - 1]);
                }

                Assert.Equal(3, viewModel.WorldClockItems.Count);

                // Reduce to 1 entry first
                source.RemoveWorldClockCommand.Execute(source.WorldClockEntries[2]);
                source.RemoveWorldClockCommand.Execute(source.WorldClockEntries[1]);
                Assert.Single(viewModel.WorldClockItems);

                // Add entry when count < 3
                source.AddWorldClockCommand.Execute(null);
                Assert.Equal(2, viewModel.WorldClockItems.Count);

                source.AddWorldClockCommand.Execute(null);
                Assert.Equal(3, viewModel.WorldClockItems.Count);

                // Adding 4th should be suppressed (max 3)
                source.AddWorldClockCommand.Execute(null);
                Assert.Equal(3, viewModel.WorldClockItems.Count);

                // Move item up/down
                var firstItem = source.WorldClockEntries[0];
                var secondItem = source.WorldClockEntries[1];
                string firstLabel = firstItem.Label;
                string secondLabel = secondItem.Label;

                source.MoveWorldClockDownCommand.Execute(firstItem);
                Assert.Same(boundCollection, viewModel.WorldClockItems);
                Assert.Equal(secondLabel.ToUpperInvariant(), viewModel.WorldClockItems[0].Label);
                Assert.Equal(firstLabel.ToUpperInvariant(), viewModel.WorldClockItems[1].Label);
            }
            finally
            {
                viewModel.Dispose();
                source.Music.Cleanup();
            }
        });
    }

    private sealed class TestMainViewModel : MainViewModel
    {
        internal void RaisePropertyChanged(string? propertyName) =>
            OnPropertyChanged(propertyName);
    }
}
