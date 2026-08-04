using System.Text.Json;
using NoraBar.Hud;
using NoraBar.Hud.Launcher;
using NoraBar.Services;
using Xunit;

namespace NoraBar.Tests.Settings;

public sealed class LauncherHudSettingsJsonTests
{
    [Fact]
    public void Read_MissingPayload_ReturnsEmptyDefaultPage()
    {
        LauncherHudSettings result = LauncherHudSettingsJson.Read(new UserSettings());

        Assert.Equal(LauncherHudSettings.CurrentPayloadVersion, result.PayloadVersion);
        LauncherPage page = Assert.Single(result.Pages);
        Assert.Empty(page.Groups.SelectMany(group => group.Items));
        Assert.True(result.SmartEnabled);
        Assert.Null(result.OpenShortcut);
        Assert.Null(result.SearchShortcut);
    }

    [Fact]
    public void Write_PreservesUnknownLauncherPayloadProperties()
    {
        var settings = new UserSettings
        {
            Modules = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                [BuiltInHudIds.Launcher] = JsonSerializer.SerializeToElement(new
                {
                    PayloadVersion = 99,
                    FutureOption = new { mode = "future" }
                })
            }
        };

        LauncherHudSettingsJson.Write(settings, LauncherHudSettings.Default);

        JsonElement payload = settings.Modules[BuiltInHudIds.Launcher];
        Assert.Equal("future", payload.GetProperty("FutureOption").GetProperty("mode").GetString());
        Assert.Equal(99, payload.GetProperty("PayloadVersion").GetInt32());
    }

    [Fact]
    public void Read_MalformedPayload_ReturnsDefaults()
    {
        var settings = new UserSettings
        {
            Modules = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                [BuiltInHudIds.Launcher] = JsonSerializer.SerializeToElement("broken")
            }
        };

        LauncherHudSettings result = LauncherHudSettingsJson.Read(settings);

        Assert.Equal(LauncherHudSettings.Default, result);
    }

    [Fact]
    public void ReadAndWrite_PreservesStableHierarchyAndItemKinds()
    {
        var item = new LauncherItem(
            "item-browser",
            "Browser",
            LauncherItemKind.Win32Application,
            @"C:\Apps\browser.exe",
            Arguments: "--profile work",
            WorkingDirectory: @"C:\Apps",
            RunAsAdministrator: true,
            ExistingInstanceBehavior: LauncherExistingInstanceBehavior.FocusExisting);
        var expected = LauncherHudSettings.Default with
        {
            Pages = [new LauncherPage("page-work", "Work", [new LauncherGroup("group-main", "Main", [item])])]
        };
        var settings = new UserSettings();

        LauncherHudSettingsJson.Write(settings, expected);
        LauncherHudSettings actual = LauncherHudSettingsJson.Read(settings);

        Assert.Equal(expected.PayloadVersion, actual.PayloadVersion);
        Assert.Equal(expected.SmartEnabled, actual.SmartEnabled);
        LauncherPage actualPage = Assert.Single(actual.Pages);
        Assert.Equal("page-work", actualPage.Id);
        Assert.Equal("Work", actualPage.DisplayName);
        LauncherItem actualItem = Assert.Single(Assert.Single(actualPage.Groups).Items);
        Assert.Equal(item, actualItem);
    }
}
