using System.ComponentModel;
using System.Windows.Threading;
using NoraBar.Hud.Home;
using NoraBar.ViewModels;
using Xunit;

namespace NoraBar.Tests.Hud;

public sealed class HomeHudViewModelTests
{
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
                Assert.Equal(1, invalidationCount);
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