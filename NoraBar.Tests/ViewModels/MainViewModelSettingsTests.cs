using System.Text.Json;
using NoraBar.Hud;
using NoraBar.Models;
using NoraBar.Services;
using NoraBar.ViewModels;
using Xunit;

namespace NoraBar.Tests.ViewModels;

public class MainViewModelSettingsTests
{
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
        var viewModel = new MainViewModel();
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
        var viewModel = new MainViewModel();
        var initial = viewModel.ActiveHomeWidgets.ToList();
        if (initial.Count >= 2)
        {
            var item0 = initial[0];
            var item1 = initial[1];

            List<NoraBar.Hud.Home.Widgets.HomeWidgetConfig> reordered = [item1, item0, .. initial.Skip(2)];
            viewModel.ActiveHomeWidgets = reordered.AsReadOnly();

            Assert.Equal(item1.Id, viewModel.ActiveHomeWidgets[0].Id);
            Assert.Equal(item0.Id, viewModel.ActiveHomeWidgets[1].Id);
        }
    }
}
