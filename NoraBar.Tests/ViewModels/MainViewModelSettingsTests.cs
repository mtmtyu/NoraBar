using System.Text.Json;
using NoraBar.Hud;
using NoraBar.Models;
using NoraBar.Services;
using NoraBar.Tests.Hud;
using NoraBar.ViewModels;
using Xunit;

namespace NoraBar.Tests.ViewModels;

public class MainViewModelSettingsTests : IDisposable
{
    private readonly string _tempDirectoryPath;
    private readonly FileSettingsStore _settingsStore;

    public MainViewModelSettingsTests()
    {
        _tempDirectoryPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "NoraBar_TestSettings_" + System.Guid.NewGuid().ToString("N"));
        _settingsStore = new FileSettingsStore(
            _tempDirectoryPath,
            System.IO.Path.Combine(_tempDirectoryPath, "legacy-settings.json"),
            enableFirstRunStartup: false);
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(_tempDirectoryPath))
        {
            try
            {
                System.IO.Directory.Delete(_tempDirectoryPath, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public void Constructor_LoadsOnlyFromInjectedTemporaryStore()
    {
        _settingsStore.Save(new UserSettings
        {
            Language = AppLanguage.English,
            ShowProgressBar = false,
            ShowLyrics = true
        });

        MainViewModel viewModel = CreateViewModel();

        Assert.StartsWith(
            _tempDirectoryPath,
            _settingsStore.SettingsFilePath,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(AppLanguage.English, viewModel.SelectedLanguage);
        Assert.False(viewModel.ShowProgressBar);
        Assert.True(viewModel.ShowLyrics);
    }

    [Fact]
    public void UpdateKnownSettings_PreservesHudModuleConfiguration()
    {
        List<string> enabledHudModuleIds = ["com.example.weather"];
        var modules = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["com.example.weather"] = JsonSerializer.SerializeToElement(new { city = "Tokyo" })
        };
        var additionalProperties = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["FutureSetting"] = JsonSerializer.SerializeToElement(new { enabled = true })
        };
        var settings = new UserSettings
        {
            DefaultHudId = "com.example.weather",
            EnabledHudModuleIds = enabledHudModuleIds,
            Modules = modules,
            AdditionalProperties = additionalProperties
        };

        MainViewModel.UpdateKnownSettings(
            settings,
            DesignVariant.ProductivityCommandIsland,
            showProgressBar: false,
            showLyrics: true,
            TextScrollMode.HoverOnly,
            AppLanguage.English,
            hasCustomPosition: true,
            windowLeft: 120.5,
            windowTop: 240.5,
            checkUpdateOnStartup: false,
            disableExpandOnFullscreen: false);

        Assert.Equal(DesignVariant.ProductivityCommandIsland, settings.Variant);
        Assert.False(settings.ShowProgressBar);
        Assert.True(settings.ShowLyrics);
        Assert.Equal(TextScrollMode.HoverOnly, settings.TextScrollMode);
        Assert.Equal(AppLanguage.English, settings.Language);
        Assert.True(settings.HasCustomPosition);
        Assert.Equal(120.5, settings.WindowLeft);
        Assert.Equal(240.5, settings.WindowTop);
        Assert.False(settings.CheckUpdateOnStartup);
        Assert.False(settings.DisableExpandOnFullscreen);
        Assert.Equal("com.example.weather", settings.DefaultHudId);
        Assert.Same(enabledHudModuleIds, settings.EnabledHudModuleIds);
        Assert.Same(modules, settings.Modules);
        Assert.Equal("Tokyo", settings.Modules["com.example.weather"].GetProperty("city").GetString());
        Assert.Same(additionalProperties, settings.AdditionalProperties);
        Assert.True(settings.AdditionalProperties["FutureSetting"].GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public void UpdateKnownSettings_PreservesUnknownFutureDesignVariantValue()
    {
        DesignVariant futureVariant = (DesignVariant)99;
        var settings = new UserSettings
        {
            Variant = futureVariant
        };

        MainViewModel.UpdateKnownSettings(
            settings,
            settings.Variant,
            showProgressBar: false,
            showLyrics: true,
            TextScrollMode.HoverOnly,
            AppLanguage.English,
            hasCustomPosition: true,
            windowLeft: 120.5,
            windowTop: 240.5,
            checkUpdateOnStartup: false,
            disableExpandOnFullscreen: false);

        Assert.Equal(futureVariant, settings.Variant);
        Assert.False(settings.ShowProgressBar);
    }

    [Fact]
    public void ResetKnownSettings_ResetsCurrentHudSettingsAndPreservesUnknownJson()
    {
        JsonElement futureMusicSettings = JsonSerializer.SerializeToElement(new
        {
            visualization = "spectrum"
        });
        JsonElement futureModuleSettings = JsonSerializer.SerializeToElement(new
        {
            city = "Tokyo"
        });
        JsonElement futureTopLevelSetting = JsonSerializer.SerializeToElement(new
        {
            enabled = true
        });
        var modules = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            [BuiltInHudIds.Music] = futureMusicSettings,
            ["com.example.weather"] = futureModuleSettings
        };
        var additionalProperties = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["FutureSetting"] = futureTopLevelSetting
        };
        var settings = new UserSettings
        {
            SchemaVersion = 99,
            DefaultHudId = "com.example.weather",
            EnabledHudModuleIds = ["com.example.weather", BuiltInHudIds.Music],
            Modules = modules,
            AdditionalProperties = additionalProperties,
            Variant = DesignVariant.LyricsFocusedSidebar,
            ShowProgressBar = false,
            ShowLyrics = true,
            TextScrollMode = TextScrollMode.HoverOnly,
            Language = AppLanguage.English,
            HasCustomPosition = true,
            WindowLeft = 120.5,
            WindowTop = 240.5,
            CheckUpdateOnStartup = false,
            DisableExpandOnFullscreen = false
        };
        var defaults = new UserSettings();

        MainViewModel.ResetKnownSettings(settings);

        Assert.Equal(UserSettings.CurrentSchemaVersion, settings.SchemaVersion);
        Assert.Equal(BuiltInHudIds.Music, settings.DefaultHudId);
        Assert.Equal([BuiltInHudIds.Music, BuiltInHudIds.Home], settings.EnabledHudModuleIds);
        Assert.Equal(defaults.Variant, settings.Variant);
        Assert.Equal(defaults.ShowProgressBar, settings.ShowProgressBar);
        Assert.Equal(defaults.ShowLyrics, settings.ShowLyrics);
        Assert.Equal(defaults.TextScrollMode, settings.TextScrollMode);
        Assert.Equal(defaults.Language, settings.Language);
        Assert.Equal(defaults.HasCustomPosition, settings.HasCustomPosition);
        Assert.Equal(defaults.WindowLeft, settings.WindowLeft);
        Assert.Equal(defaults.WindowTop, settings.WindowTop);
        Assert.Equal(defaults.CheckUpdateOnStartup, settings.CheckUpdateOnStartup);
        Assert.Equal(defaults.DisableExpandOnFullscreen, settings.DisableExpandOnFullscreen);
        Assert.Same(modules, settings.Modules);
        Assert.Equal("spectrum", settings.Modules[BuiltInHudIds.Music].GetProperty("visualization").GetString());
        Assert.Equal("Tokyo", settings.Modules["com.example.weather"].GetProperty("city").GetString());
        Assert.Same(additionalProperties, settings.AdditionalProperties);
        Assert.True(settings.AdditionalProperties["FutureSetting"].GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public void IsWidgetEditMode_TogglesStateAndClearsIsPositionEditMode()
    {
        MainViewModel viewModel = CreateViewModel();
        viewModel.IsPositionEditMode = true;

        Assert.True(viewModel.IsPositionEditMode);
        Assert.False(viewModel.IsWidgetEditMode);

        viewModel.IsWidgetEditMode = true;

        Assert.True(viewModel.IsWidgetEditMode);
        Assert.False(viewModel.IsPositionEditMode);
    }

    [Fact]
    public void ActiveHomeWidgets_ReordersWidgetsCorrectly()
    {
        MainViewModel viewModel = CreateViewModel();
        var initial = viewModel.ActiveHomeWidgets.ToList();
        Assert.True(initial.Count >= 2);
        var item0 = initial[0];
        var item1 = initial[1];

        List<NoraBar.Hud.Home.Widgets.HomeWidgetConfig> reordered = [item1, item0, .. initial.Skip(2)];
        viewModel.ActiveHomeWidgets = reordered.AsReadOnly();

        Assert.Equal(item1.Id, viewModel.ActiveHomeWidgets[0].Id);
        Assert.Equal(item0.Id, viewModel.ActiveHomeWidgets[1].Id);
    }

    [Fact]
    public void AvailableTimeZones_IsSharedAndReadOnlyAcrossViewModels()
    {
        MainViewModel first = CreateViewModel();
        MainViewModel second = CreateViewModel();

        Assert.Same(first.AvailableTimeZones, second.AvailableTimeZones);
        Assert.IsAssignableFrom<System.Collections.ObjectModel.ReadOnlyCollection<MainViewModel.TimeZoneOption>>(
            first.AvailableTimeZones);
    }

    [Fact]
    public async Task EnterWidgetEditModeAsync_NavigatesToHomeHudAndEnablesEditMode()
    {
        var music = new FakeHudModule(BuiltInHudIds.Music);
        var home = new FakeHudModule(BuiltInHudIds.Home);
        var registry = new HudRegistry();
        registry.Register(music);
        registry.Register(home);

        var router = new HudRouter(
            registry,
            BuiltInHudIds.Music,
            [BuiltInHudIds.Music, BuiltInHudIds.Home]);
        await router.InitializeAsync(CancellationToken.None);

        var settings = new UserSettings();
        var navigation = new HudNavigationViewModel(
            router,
            [music, home],
            settings,
            AppLanguage.Japanese,
            () => { });

        MainViewModel viewModel = CreateViewModel();
        viewModel.AttachHudNavigation(navigation);

        Assert.Equal(BuiltInHudIds.Music, router.CurrentHudId);
        Assert.False(viewModel.IsWidgetEditMode);

        await viewModel.EnterWidgetEditModeAsync();

        Assert.Equal(BuiltInHudIds.Home, router.CurrentHudId);
        Assert.True(viewModel.IsWidgetEditMode);
    }

    [Fact]
    public async Task EnterWidgetEditModeAsync_EnablesHomeHudIfDisabledAndNavigates()
    {
        var music = new FakeHudModule(BuiltInHudIds.Music);
        var home = new FakeHudModule(BuiltInHudIds.Home);
        var registry = new HudRegistry();
        registry.Register(music);
        registry.Register(home);

        var router = new HudRouter(
            registry,
            BuiltInHudIds.Music,
            [BuiltInHudIds.Music]);
        await router.InitializeAsync(CancellationToken.None);

        var settings = new UserSettings { EnabledHudModuleIds = [BuiltInHudIds.Music] };
        var navigation = new HudNavigationViewModel(
            router,
            [music, home],
            settings,
            AppLanguage.Japanese,
            () => { });

        MainViewModel viewModel = CreateViewModel();
        viewModel.AttachHudNavigation(navigation);

        Assert.Equal(BuiltInHudIds.Music, router.CurrentHudId);
        HudNavigationItemViewModel homeItem = navigation.Items.Single(item => item.Id == BuiltInHudIds.Home);
        Assert.False(homeItem.IsEnabled);

        await viewModel.EnterWidgetEditModeAsync();

        Assert.True(homeItem.IsEnabled);
        Assert.Equal(BuiltInHudIds.Home, router.CurrentHudId);
        Assert.True(viewModel.IsWidgetEditMode);
    }

    [Fact]
    public void ReorderWorldClock_MovesWorldClockEntriesCorrectly()
    {
        MainViewModel viewModel = CreateViewModel();
        viewModel.WorldClockEntries.Clear();
        var item1 = new WorldClockEntryViewModel("NYC", "UTC", () => { });
        var item2 = new WorldClockEntryViewModel("LON", "UTC", () => { });
        viewModel.WorldClockEntries.Add(item1);
        viewModel.WorldClockEntries.Add(item2);

        viewModel.ReorderWorldClock(0, 1);

        Assert.Equal("LON", viewModel.WorldClockEntries[0].Label);
        Assert.Equal("NYC", viewModel.WorldClockEntries[1].Label);
    }

    [Fact]
    public void AddWorldClock_UsesSelectedTimeZoneLabel_NotNew()
    {
        MainViewModel viewModel = CreateViewModel();
        viewModel.WorldClockEntries.Clear();

        viewModel.AddWorldClockCommand.Execute(null);

        Assert.Single(viewModel.WorldClockEntries);
        Assert.NotEqual("NEW", viewModel.WorldClockEntries[0].Label);
        Assert.Equal("UTC", viewModel.WorldClockEntries[0].Label);
    }

    [Fact]
    public void WorldClockEntry_ChangingTimeZoneId_UpdatesLabelToMatchTimeZoneAbbreviation()
    {
        var item = new WorldClockEntryViewModel("UTC", "UTC", () => { });

        item.TimeZoneId = "Tokyo Standard Time";

        Assert.Equal("JST", item.Label);
    }

    [Fact]
    public void WorldClockEntry_ChangingTimeZoneId_InvokesCallbackOnceAndNotifiesBothProperties()
    {
        int callbackCount = 0;
        var changedProperties = new List<string?>();
        var item = new WorldClockEntryViewModel("UTC", "UTC", () => callbackCount++);
        item.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        item.TimeZoneId = "Tokyo Standard Time";

        Assert.Equal(1, callbackCount);
        Assert.Contains(nameof(WorldClockEntryViewModel.TimeZoneId), changedProperties);
        Assert.Contains(nameof(WorldClockEntryViewModel.Label), changedProperties);

        item.Label = "TOKYO";
        Assert.Equal(2, callbackCount);
    }

    [Fact]
    public void GetDefaultLabelForTimeZone_ReturnsExpectedAbbreviations()
    {
        Assert.Equal("UTC", WorldClockEntryViewModel.GetDefaultLabelForTimeZone("UTC"));
        Assert.Equal("JST", WorldClockEntryViewModel.GetDefaultLabelForTimeZone("Tokyo Standard Time"));
        Assert.Equal("EST", WorldClockEntryViewModel.GetDefaultLabelForTimeZone("Eastern Standard Time"));
        Assert.Equal("PST", WorldClockEntryViewModel.GetDefaultLabelForTimeZone("Pacific Standard Time"));
        Assert.Equal("GMT", WorldClockEntryViewModel.GetDefaultLabelForTimeZone("GMT Standard Time"));
    }

    private MainViewModel CreateViewModel() => new(_settingsStore);
}
