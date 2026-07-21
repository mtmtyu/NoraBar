using NoraBar.Hud;
using NoraBar.Models;
using NoraBar.Services;
using NoraBar.Tests.Hud;
using NoraBar.ViewModels;
using Xunit;

namespace NoraBar.Tests.ViewModels;

public sealed class HudNavigationViewModelTests
{
    [Fact]
    public async Task NavigateRelativeAsync_WrapsAcrossEnabledModules()
    {
        var music = new FakeHudModule(BuiltInHudIds.Music);
        var home = new FakeHudModule(BuiltInHudIds.Home);
        HudRouter router = CreateRouter(music, home);
        await router.InitializeAsync(CancellationToken.None);
        var settings = new UserSettings();
        var navigation = new HudNavigationViewModel(
            router,
            [music, home],
            settings,
            AppLanguage.Japanese,
            () => { });

        await navigation.NavigateRelativeAsync(1);
        Assert.Equal(BuiltInHudIds.Home, router.CurrentHudId);

        await navigation.NavigateRelativeAsync(1);
        Assert.Equal(BuiltInHudIds.Music, router.CurrentHudId);
    }

    [Fact]
    public async Task SetEnabledAsync_DisablingCurrentSelectsFirstEnabledAndUpdatesDefault()
    {
        var music = new FakeHudModule(BuiltInHudIds.Music);
        var home = new FakeHudModule(BuiltInHudIds.Home);
        HudRouter router = CreateRouter(music, home);
        await router.InitializeAsync(CancellationToken.None);
        var settings = new UserSettings();
        var navigation = new HudNavigationViewModel(
            router,
            [music, home],
            settings,
            AppLanguage.Japanese,
            () => { });

        await navigation.SetEnabledAsync(BuiltInHudIds.Music, false);

        Assert.Equal([BuiltInHudIds.Home], settings.EnabledHudModuleIds);
        Assert.Equal(BuiltInHudIds.Home, settings.DefaultHudId);
        Assert.Equal(BuiltInHudIds.Home, router.CurrentHudId);
    }

    [Fact]
    public async Task MoveAsync_PersistsEnabledNavigationOrder()
    {
        var music = new FakeHudModule(BuiltInHudIds.Music);
        var home = new FakeHudModule(BuiltInHudIds.Home);
        HudRouter router = CreateRouter(music, home);
        await router.InitializeAsync(CancellationToken.None);
        var settings = new UserSettings();
        var navigation = new HudNavigationViewModel(
            router,
            [music, home],
            settings,
            AppLanguage.English,
            () => { });

        await navigation.MoveAsync(BuiltInHudIds.Home, -1);

        Assert.Equal([BuiltInHudIds.Home, BuiltInHudIds.Music], settings.EnabledHudModuleIds);
        Assert.Equal(BuiltInHudIds.Home, navigation.Items[0].Id);
    }

    [Fact]
    public async Task SetEnabledAsync_RejectingLastModuleRefreshesBindingValue()
    {
        var music = new FakeHudModule(BuiltInHudIds.Music);
        var home = new FakeHudModule(BuiltInHudIds.Home);
        HudRouter router = CreateRouter(music, home);
        await router.InitializeAsync(CancellationToken.None);
        var navigation = new HudNavigationViewModel(
            router,
            [music, home],
            new UserSettings(),
            AppLanguage.Japanese,
            () => { });
        await navigation.SetEnabledAsync(BuiltInHudIds.Music, false);
        HudNavigationItemViewModel homeItem = navigation.Items.Single(item => item.Id == BuiltInHudIds.Home);
        int notificationCount = 0;
        homeItem.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(HudNavigationItemViewModel.IsEnabled))
            {
                notificationCount++;
            }
        };

        await navigation.SetEnabledAsync(BuiltInHudIds.Home, false);

        Assert.True(homeItem.IsEnabled);
        Assert.Equal(1, notificationCount);
    }

    [Fact]
    public async Task ResetToDefaultsAsync_RestoresEnabledOrderAndStartupHud()
    {
        var music = new FakeHudModule(BuiltInHudIds.Music);
        var home = new FakeHudModule(BuiltInHudIds.Home);
        HudRouter router = CreateRouter(music, home);
        await router.InitializeAsync(CancellationToken.None);
        var settings = new UserSettings();
        var navigation = new HudNavigationViewModel(
            router,
            [music, home],
            settings,
            AppLanguage.Japanese,
            () => { });
        await navigation.MoveAsync(BuiltInHudIds.Home, -1);
        await navigation.SetEnabledAsync(BuiltInHudIds.Music, false);

        await navigation.ResetToDefaultsAsync();

        Assert.Equal([BuiltInHudIds.Music, BuiltInHudIds.Home], settings.EnabledHudModuleIds);
        Assert.Equal(BuiltInHudIds.Music, settings.DefaultHudId);
        Assert.Equal(BuiltInHudIds.Home, router.CurrentHudId);
        Assert.Equal(BuiltInHudIds.Music, navigation.Items[0].Id);
        Assert.All(navigation.Items, item => Assert.True(item.IsEnabled));
    }

    private static HudRouter CreateRouter(params FakeHudModule[] modules)
    {
        var registry = new HudRegistry();
        foreach (FakeHudModule module in modules)
        {
            registry.Register(module);
        }

        return new HudRouter(
            registry,
            BuiltInHudIds.Music,
            [BuiltInHudIds.Music, BuiltInHudIds.Home]);
    }
}
