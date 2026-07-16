using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using NoraBar.Models;
using NoraBar.Services;
using Xunit;

namespace NoraBar.Tests.Settings;

public class UserSettingsJsonTests
{
    [Fact]
    public void Serialize_RoundTripPreservesEveryKnownSetting()
    {
        AppLanguage expectedLanguage = new UserSettings().Language == AppLanguage.Japanese
            ? AppLanguage.English
            : AppLanguage.Japanese;
        string json = $$"""
            {
              "SchemaVersion": 7,
              "DefaultHudId": "com.example.weather",
              "EnabledHudModuleIds": ["com.example.weather", "music"],
              "Modules": {
                "music": { "accent": "violet", "opacity": 0.75 },
                "com.example.weather": { "city": "Sapporo" }
              },
              "Variant": 2,
              "ShowProgressBar": false,
              "Language": {{(int)expectedLanguage}},
              "ShowLyrics": true,
              "TextScrollMode": 2,
              "HasCustomPosition": true,
              "WindowLeft": 321.25,
              "WindowTop": -45.5,
              "CheckUpdateOnStartup": false,
              "DisableExpandOnFullscreen": false,
              "FutureSetting": { "mode": "experimental", "revision": 42 }
            }
            """;

        using JsonDocument input = JsonDocument.Parse(json);
        HashSet<string> inputProperties = input.RootElement
            .EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
        string[] knownSerializedProperties = typeof(UserSettings)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(IsKnownSerializedProperty)
            .Select(GetSerializedPropertyName)
            .ToArray();

        Assert.All(knownSerializedProperties, property => Assert.Contains(property, inputProperties));

        UserSettings loaded = UserSettingsJson.DeserializeOrDefault(json);
        UserSettings result = UserSettingsJson.DeserializeOrDefault(UserSettingsJson.Serialize(loaded));

        AssertKnownSerializedPropertiesEqual(input.RootElement, result);

        Assert.Equal(7, result.SchemaVersion);
        Assert.Equal("com.example.weather", result.DefaultHudId);
        Assert.Equal(["com.example.weather", "music"], result.EnabledHudModuleIds);
        Assert.Equal("violet", result.Modules["music"].GetProperty("accent").GetString());
        Assert.Equal(0.75, result.Modules["music"].GetProperty("opacity").GetDouble());
        Assert.Equal("Sapporo", result.Modules["com.example.weather"].GetProperty("city").GetString());
        Assert.Equal(DesignVariant.LyricsFocusedSidebar, result.Variant);
        Assert.False(result.ShowProgressBar);
        Assert.Equal(expectedLanguage, result.Language);
        Assert.True(result.ShowLyrics);
        Assert.Equal(TextScrollMode.HoverOnly, result.TextScrollMode);
        Assert.True(result.HasCustomPosition);
        Assert.Equal(321.25, result.WindowLeft);
        Assert.Equal(-45.5, result.WindowTop);
        Assert.False(result.CheckUpdateOnStartup);
        Assert.False(result.DisableExpandOnFullscreen);
        Assert.Equal("experimental", result.AdditionalProperties["FutureSetting"].GetProperty("mode").GetString());
        Assert.Equal(42, result.AdditionalProperties["FutureSetting"].GetProperty("revision").GetInt32());
    }

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

    [Fact]
    public void Serialize_PreservesUnknownFutureDesignVariantValue()
    {
        const int futureVariantValue = 99;
        const string json = """
            {
              "Variant": 99,
              "ShowProgressBar": true
            }
            """;

        UserSettings loaded = UserSettingsJson.DeserializeOrDefault(json);
        UserSettings result = UserSettingsJson.DeserializeOrDefault(UserSettingsJson.Serialize(loaded));

        Assert.Equal(futureVariantValue, (int)loaded.Variant);
        Assert.Equal(futureVariantValue, (int)result.Variant);
    }

    [Fact]
    public void KnownSerializedPropertyFilter_IncludesConditionallyIgnoredProperties()
    {
        HashSet<string> knownProperties = typeof(SerializationPropertyFixture)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(IsKnownSerializedProperty)
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(nameof(SerializationPropertyFixture.Included), knownProperties);
        Assert.Contains(nameof(SerializationPropertyFixture.WhenWritingDefault), knownProperties);
        Assert.Contains(nameof(SerializationPropertyFixture.WhenWritingNull), knownProperties);
        Assert.DoesNotContain(nameof(SerializationPropertyFixture.AlwaysIgnored), knownProperties);
        Assert.DoesNotContain(nameof(SerializationPropertyFixture.ExtensionData), knownProperties);
    }

    private static void AssertKnownSerializedPropertiesEqual(JsonElement expectedJson, UserSettings actual)
    {
        using JsonDocument actualDocument = JsonDocument.Parse(UserSettingsJson.Serialize(actual));
        JsonElement actualJson = actualDocument.RootElement;
        PropertyInfo[] knownSerializedProperties = typeof(UserSettings)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(IsKnownSerializedProperty)
            .ToArray();

        foreach (PropertyInfo property in knownSerializedProperties)
        {
            string serializedName = GetSerializedPropertyName(property);
            JsonElement expectedValue = expectedJson.GetProperty(serializedName);
            JsonElement actualValue = actualJson.GetProperty(serializedName);

            Assert.True(
                JsonElement.DeepEquals(expectedValue, actualValue),
                $"Known setting '{serializedName}' changed during serialization. " +
                $"Expected: {expectedValue.GetRawText()}; Actual: {actualValue.GetRawText()}.");
        }
    }

    private static bool IsKnownSerializedProperty(PropertyInfo property)
    {
        if (property.GetCustomAttribute<JsonExtensionDataAttribute>() is not null)
        {
            return false;
        }

        JsonIgnoreAttribute? ignoreAttribute = property.GetCustomAttribute<JsonIgnoreAttribute>();
        return ignoreAttribute is null || ignoreAttribute.Condition != JsonIgnoreCondition.Always;
    }

    private static string GetSerializedPropertyName(PropertyInfo property)
    {
        return property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
            ?? property.Name;
    }

    private sealed class SerializationPropertyFixture
    {
        public int Included { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int WhenWritingDefault { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? WhenWritingNull { get; set; }

        [JsonIgnore]
        public int AlwaysIgnored { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement> ExtensionData { get; set; } = [];
    }
}
