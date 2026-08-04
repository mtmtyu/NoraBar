using System.Text.Json;
using NoraBar.Hud;
using NoraBar.Services;
using Xunit;

namespace NoraBar.Tests.Settings;

public sealed class LauncherIntroductionMigrationTests
{
    [Fact]
    public void NewDefaults_EnableMusicHomeAndLauncherWithMusicAsDefault()
    {
        var settings = new UserSettings();

        Assert.Equal(BuiltInHudIds.Music, settings.DefaultHudId);
        Assert.Equal(
            [BuiltInHudIds.Music, BuiltInHudIds.Home, BuiltInHudIds.Launcher],
            settings.EnabledHudModuleIds);
        Assert.True(settings.LauncherHudIntroductionCompleted);
    }

    [Fact]
    public void Deserialize_OldSettingsIntroducesLauncherOnceAndPreservesUnknownIds()
    {
        const string json = """
            {
              "DefaultHudId": "music",
              "EnabledHudModuleIds": ["com.example.weather", "music", "home"],
              "HomeHudIntroductionCompleted": true
            }
            """;

        UserSettings result = UserSettingsJson.DeserializeOrDefault(json);

        Assert.Equal(
            ["com.example.weather", BuiltInHudIds.Music, BuiltInHudIds.Home, BuiltInHudIds.Launcher],
            result.EnabledHudModuleIds);
        Assert.True(result.LauncherHudIntroductionCompleted);
    }

    [Fact]
    public void Serialize_AfterIntroductionPreservesManualLauncherDisable()
    {
        var settings = new UserSettings
        {
            EnabledHudModuleIds = [BuiltInHudIds.Music, BuiltInHudIds.Home],
            LauncherHudIntroductionCompleted = true
        };

        UserSettings result = UserSettingsJson.DeserializeOrDefault(UserSettingsJson.Serialize(settings));

        Assert.DoesNotContain(BuiltInHudIds.Launcher, result.EnabledHudModuleIds);
    }

    [Fact]
    public void IntroductionPreservesUnknownModuleAndTopLevelData()
    {
        const string json = """
            {
              "EnabledHudModuleIds": ["music", "home"],
              "HomeHudIntroductionCompleted": true,
              "Modules": { "com.example.future": { "answer": 42 } },
              "FutureTopLevel": { "enabled": true }
            }
            """;

        UserSettings loaded = UserSettingsJson.DeserializeOrDefault(json);
        using JsonDocument saved = JsonDocument.Parse(UserSettingsJson.Serialize(loaded));

        Assert.Equal(42, saved.RootElement.GetProperty("Modules")
            .GetProperty("com.example.future").GetProperty("answer").GetInt32());
        Assert.True(saved.RootElement.GetProperty("FutureTopLevel").GetProperty("enabled").GetBoolean());
    }
}
