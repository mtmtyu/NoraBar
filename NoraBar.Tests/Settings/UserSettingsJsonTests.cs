using NoraBar.Models;
using NoraBar.Services;
using Xunit;

namespace NoraBar.Tests.Settings;

public class UserSettingsJsonTests
{
    [Fact]
    public void DeserializeOrDefault_LoadsVersionlessSettingsAndAddsNewDefaults()
    {
        const string json = """
            {
              "Variant": 1,
              "ShowProgressBar": false,
              "Language": 1,
              "ShowLyrics": true,
              "WindowLeft": 120.5
            }
            """;

        UserSettings result = UserSettingsJson.DeserializeOrDefault(json);

        Assert.Equal(UserSettings.CurrentSchemaVersion, result.SchemaVersion);
        Assert.Equal("music", result.DefaultHudId);
        Assert.Equal(new[] { "music" }, result.EnabledHudModuleIds);
        Assert.Equal(DesignVariant.ProductivityCommandIsland, result.Variant);
        Assert.False(result.ShowProgressBar);
        Assert.True(result.ShowLyrics);
        Assert.Equal(120.5, result.WindowLeft);
    }

    [Fact]
    public void Serialize_PreservesUnknownHudIdsAndModuleJson()
    {
        const string json = """
            {
              "SchemaVersion": 1,
              "DefaultHudId": "com.example.weather",
              "EnabledHudModuleIds": ["com.example.weather"],
              "Modules": {
                "com.example.weather": { "city": "Tokyo", "units": "metric" }
              },
              "FutureSetting": { "enabled": true }
            }
            """;

        UserSettings loaded = UserSettingsJson.DeserializeOrDefault(json);
        UserSettings result = UserSettingsJson.DeserializeOrDefault(UserSettingsJson.Serialize(loaded));

        Assert.Equal("com.example.weather", result.DefaultHudId);
        Assert.Equal(new[] { "com.example.weather" }, result.EnabledHudModuleIds);
        Assert.Equal("Tokyo", result.Modules["com.example.weather"].GetProperty("city").GetString());
        Assert.True(result.AdditionalProperties["FutureSetting"].GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public void DeserializeOrDefault_ReturnsDefaultsForBrokenJson()
    {
        UserSettings result = UserSettingsJson.DeserializeOrDefault("{ broken");

        Assert.Equal("music", result.DefaultHudId);
        Assert.Equal(new[] { "music" }, result.EnabledHudModuleIds);
    }

    [Fact]
    public void DeserializeOrDefault_PreservesFutureSchemaVersion()
    {
        UserSettings result = UserSettingsJson.DeserializeOrDefault(
            """{ "SchemaVersion": 99, "FutureSetting": true }""");

        Assert.Equal(99, result.SchemaVersion);
        Assert.True(result.AdditionalProperties["FutureSetting"].GetBoolean());
    }

    [Fact]
    public void Serialize_DoesNotMutateCallerSettings()
    {
        var settings = new UserSettings
        {
            SchemaVersion = 0,
            DefaultHudId = "com.example.weather",
            EnabledHudModuleIds = []
        };

        _ = UserSettingsJson.Serialize(settings);

        Assert.Equal(0, settings.SchemaVersion);
        Assert.Equal("com.example.weather", settings.DefaultHudId);
        Assert.Empty(settings.EnabledHudModuleIds);
    }
}
