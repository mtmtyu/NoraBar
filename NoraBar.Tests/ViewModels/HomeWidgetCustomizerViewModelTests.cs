using NoraBar.Hud.Home.Widgets;
using NoraBar.ViewModels;
using Xunit;

namespace NoraBar.Tests.ViewModels;

public sealed class HomeWidgetCustomizerViewModelTests
{
    [Fact]
    public void Constructor_InitializesActiveAndCatalogWidgets()
    {
        List<HomeWidgetConfig> initial = new List<HomeWidgetConfig>
        {
            new("w1", HomeWidgetType.DigitalClock, HomeWidgetStyle.ClockMinimal),
            new("w2", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaCompact)
        };

        HomeWidgetCustomizerViewModel vm = new HomeWidgetCustomizerViewModel(initial);

        Assert.Equal(2, vm.ActiveWidgets.Count);
        Assert.Equal(4, vm.CatalogWidgets.Count);
        Assert.Equal("w1", vm.ActiveWidgets[0].Id);
    }

    [Fact]
    public void AddWidgetCommand_AppendsNewWidgetToActive()
    {
        HomeWidgetCustomizerViewModel vm = new HomeWidgetCustomizerViewModel(new List<HomeWidgetConfig>());
        HomeWidgetCustomizerItemViewModel catalogItem = vm.CatalogWidgets[0];

        vm.AddWidgetCommand.Execute(catalogItem);

        Assert.Single(vm.ActiveWidgets);
        Assert.Equal(catalogItem.Type, vm.ActiveWidgets[0].Type);
    }

    [Fact]
    public void RemoveWidgetCommand_RemovesWidgetFromActive()
    {
        List<HomeWidgetConfig> initial = new List<HomeWidgetConfig>
        {
            new("w1", HomeWidgetType.DigitalClock, HomeWidgetStyle.ClockMinimal)
        };
        HomeWidgetCustomizerViewModel vm = new HomeWidgetCustomizerViewModel(initial);

        vm.RemoveWidgetCommand.Execute(vm.ActiveWidgets[0]);

        Assert.Empty(vm.ActiveWidgets);
    }

    [Fact]
    public void MoveItem_ReordersWidgetsCorrectly()
    {
        List<HomeWidgetConfig> initial = new List<HomeWidgetConfig>
        {
            new("w1", HomeWidgetType.DigitalClock, HomeWidgetStyle.ClockMinimal),
            new("w2", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaCompact)
        };
        HomeWidgetCustomizerViewModel vm = new HomeWidgetCustomizerViewModel(initial);

        vm.MoveItem(0, 1);

        Assert.Equal("w2", vm.ActiveWidgets[0].Id);
        Assert.Equal("w1", vm.ActiveWidgets[1].Id);
    }

    [Fact]
    public void ItemStyleChangeAndCollectionChange_TriggersPreviewInvalidated()
    {
        List<HomeWidgetConfig> initial = new List<HomeWidgetConfig>
        {
            new("w1", HomeWidgetType.DigitalClock, HomeWidgetStyle.ClockMinimal)
        };
        HomeWidgetCustomizerViewModel vm = new HomeWidgetCustomizerViewModel(initial);
        int eventCount = 0;
        vm.PreviewInvalidated += (s, e) => eventCount++;

        vm.ActiveWidgets[0].Style = HomeWidgetStyle.ClockExpressive;
        Assert.True(eventCount > 0);

        int countBeforeAdd = eventCount;
        vm.AddWidgetCommand.Execute(vm.CatalogWidgets[0]);
        Assert.True(eventCount > countBeforeAdd);
    }
}
