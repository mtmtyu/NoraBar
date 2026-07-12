using System.Text.Json;
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
}
