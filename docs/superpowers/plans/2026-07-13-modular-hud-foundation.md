# Modular HUD Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 既存の音楽HUDを最初の組み込みモジュールへ移行し、MainWindowをHUD固有知識のない汎用ホストにする。

**Architecture:** `App` をComposition Rootとし、`IHudModule`、`HudRegistry`、`HudRouter` をconstructor injectionで接続する。`HudRouter` がモジュールの選択ライフサイクルと `PresentationInvalidated` の購読を所有し、`MainWindow` は `StateChanged` と `PresentationChanged` だけを監視する。既存の `MainViewModel` は音楽Viewの互換DataContextとして維持する。

**Tech Stack:** C# 14、WPF、.NET 10、System.Text.Json、xUnit 2.9.3、Microsoft.NET.Test.Sdk 17.14.1

## Global Constraints

- 対象はWindows / WPF / `net10.0-windows10.0.19041.0`。
- Base branchは `v1.1.0`、base commitは `6fb72b0272a18b0f1ff24d7160a46e01b8130faf`、working branchは `feature/phase-0-modular-hud-foundation`。
- 新しい本番用NuGetパッケージを追加しない。テスト専用依存関係だけを追加する。
- HUD IDは文字列のまま `BuiltInHudIds.Music = "music"` で定数化し、比較には `StringComparer.Ordinal` を使う。
- `Collapsed`、`Peek`、`Expanded`、`Pinned` 間の変更ではActivate／Deactivateしない。
- `MusicViewModel.Cleanup` は `MusicHudModule.DisposeAsync` だけが呼ぶ。
- Routerの状態は不変な `HudRouterSnapshot` として一度に取得し、MainWindowは通知ごとに一つのスナップショットだけを使う。
- Routerの状態読書きは短い状態ロックで同期し、イベント通知はロック外で行う。遷移中・終了中の表示状態変更は拒否する。
- Alt+F4、システムメニュー、OS Closeを含む全終了経路をAppの共通終了Taskへ統合する。
- 将来の `SchemaVersion` と未知JSONを保持し、Serialize／正規化は呼出元オブジェクトを変更しない。
- `MainWindow` は音楽View型、`DesignVariant`、`ShowLyrics`、`ShowProgressBar`、`HasMultipleSessions`、HUD ID分岐を参照しない。
- 外部DLLロード、上部タブ、新しい実用HUD、ライブアクティビティ、自動切り替えは実装しない。
- `.Result`、`.Wait()`、無制御なfire-and-forgetを追加しない。
- 各コミット前に `git status`、`git diff`、`git diff --staged` を確認する。
- push、Pull Request作成、`master` へのマージは行わない。
- `package.json` が存在しないため、npm lint/testとreact-doctorは対象外として最終報告に明記する。

---

## File Structure

### Production

- `NoraBar/Hud/BuiltInHudIds.cs`: 組み込みHUD ID。
- `NoraBar/Hud/HudPresentationState.cs`: HUDの表示状態。
- `NoraBar/Hud/HudRouterSnapshot.cs`: 初期化前・稼働中・終了後を一貫して表す不変スナップショット。
- `NoraBar/Hud/HudSize.cs`: モジュールが返す推奨サイズ。
- `NoraBar/Hud/HudViewContext.cs`: View／サイズ問い合わせの表示コンテキスト。
- `NoraBar/Hud/HudModuleMetadata.cs`: タブ等で利用するモジュールメタデータ。
- `NoraBar/Hud/IHudModule.cs`: HUDモジュール共通契約。
- `NoraBar/Hud/HudRegistry.cs`: Ordinal ID検索、登録順列挙、重複検出、破棄。
- `NoraBar/Hud/HudNavigationException.cs`: 復旧後に遷移失敗を診断可能に返す例外。
- `NoraBar/Hud/HudRouter.cs`: 有効HUD、現在HUD、表示状態、遷移、通知集約、停止。
- `NoraBar/Hud/Music/IMusicHudPresentationSource.cs`: MainViewModel依存をテスト可能にする内部境界。
- `NoraBar/Hud/Music/MainViewModelMusicHudPresentationSource.cs`: MainViewModel/MusicViewModelのアダプター。
- `NoraBar/Hud/Music/MusicHudLayout.cs`: 3デザインの純粋な推奨サイズ計算。
- `NoraBar/Hud/Music/MusicHudModule.cs`: Viewキャッシュ、無効化通知、音楽ライフサイクル。
- `NoraBar/Services/UserSettingsJson.cs`: JSON解析、構造正規化、未知JSONの往復保持。
- `NoraBar/Services/ShutdownTaskCoordinator.cs`: 複数の終了要求を同じTaskへ統合。
- `NoraBar/Services/SettingsService.cs`: 保存場所と旧ファイル移行を維持。
- `NoraBar/ViewModels/MainViewModel.cs`: 読み込んだ設定を保持して未知設定を失わず保存。
- `NoraBar/MainWindow.xaml.cs`: 汎用HUDシェルとRouter通知監視。
- `NoraBar/App.xaml`: `StartupUri` 削除、明示終了。
- `NoraBar/App.xaml.cs`: Composition Root、起動順序、共通非同期終了。

### Tests

- `NoraBar.Tests/NoraBar.Tests.csproj`: xUnitテストプロジェクト。
- `NoraBar.Tests/Settings/UserSettingsJsonTests.cs`: 旧設定、未知ID、未知JSON、壊れたJSON。
- `NoraBar.Tests/Hud/FakeHudModule.cs`: 順序と回数を記録するテストモジュール。
- `NoraBar.Tests/Hud/HudRegistryTests.cs`: 登録、検索、重複、ID検証、破棄。
- `NoraBar.Tests/Hud/HudRouterTests.cs`: 初期化、状態、フォールバック、失敗復旧、通知保留。
- `NoraBar.Tests/Hud/MusicHudLayoutTests.cs`: 3デザインのサイズ。
- `NoraBar.Tests/Hud/MusicHudModuleTests.cs`: Viewキャッシュ、購読、Cleanup一回。
- `NoraBar.Tests/Services/ShutdownTaskCoordinatorTests.cs`: 終了Task統合。
- `NoraBar.Tests/Architecture/MainWindowDependencyTests.cs`: MainWindowの依存境界。

### Documentation

- `docs/wiki/アーキテクチャ.md`: 日本語版。
- `docs/wiki/Architecture.md`: 英語版。

---

### Task 1: 後方互換な設定スキーマとテスト基盤

**Files:**
- Create: `NoraBar/Hud/BuiltInHudIds.cs`
- Create: `NoraBar.Tests/NoraBar.Tests.csproj`
- Create: `NoraBar.Tests/Settings/UserSettingsJsonTests.cs`
- Create: `NoraBar/Services/UserSettingsJson.cs`
- Modify: `NoraBar.slnx`
- Modify: `NoraBar/AssemblyInfo.cs`
- Modify: `NoraBar/Services/SettingsService.cs`
- Modify: `NoraBar/ViewModels/MainViewModel.cs`

**Interfaces:**
- Produces: `UserSettingsJson.DeserializeOrDefault(string): UserSettings`
- Produces: `UserSettingsJson.Serialize(UserSettings): string`
- Produces: `SchemaVersion`, `DefaultHudId`, `EnabledHudModuleIds`, `Modules`
- Preserves: 未知のトップレベルJSONと未知のモジュールJSON

- [ ] **Step 1: テストプロジェクトをソリューションへ追加する**

`NoraBar.Tests/NoraBar.Tests.csproj` を次の内容で作成し、`NoraBar.slnx` に `<Project Path="NoraBar.Tests/NoraBar.Tests.csproj" />` を追加する。

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <UseWPF>true</UseWPF>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\NoraBar\NoraBar.csproj" />
  </ItemGroup>
</Project>
```

`NoraBar/Hud/BuiltInHudIds.cs` に次を作成する。設定スキーマと後続の全HUD基盤はこの定数を共有する。

```csharp
public static class BuiltInHudIds
{
    public const string Music = "music";
}
```

`NoraBar/AssemblyInfo.cs` に `using System.Runtime.CompilerServices;` と `[assembly: InternalsVisibleTo("NoraBar.Tests")]` を追加し、内部のJSON処理と音楽アダプターをテスト可能にする。

- [ ] **Step 2: 設定移行の失敗テストを書く**

```csharp
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
```

- [ ] **Step 3: 設定テストを実行してREDを確認する**

Run: `dotnet test NoraBar.Tests/NoraBar.Tests.csproj --filter FullyQualifiedName~UserSettingsJsonTests`

Expected: FAIL。新しい型とプロパティが存在しないコンパイルエラー。

- [ ] **Step 4: 設定モデルと純粋JSON処理を実装する**

`UserSettings` に次を追加する。

```csharp
public const int CurrentSchemaVersion = 1;
public int SchemaVersion { get; set; } = CurrentSchemaVersion;
public string DefaultHudId { get; set; } = BuiltInHudIds.Music;
public List<string> EnabledHudModuleIds { get; set; } = [BuiltInHudIds.Music];
public Dictionary<string, JsonElement> Modules { get; set; } = new(StringComparer.Ordinal);
[JsonExtensionData]
public Dictionary<string, JsonElement> AdditionalProperties { get; set; } = new(StringComparer.Ordinal);
```

`UserSettingsJson` は `JsonException` と `NotSupportedException` だけを既定値へフォールバックする。NormalizeStructureは入力を変更せず、既知プロパティ、ID配列、Modules、AdditionalPropertiesを新しい `UserSettings` へコピーして返す。null／空白のDefaultHudIdとnull配列だけを既定値へ補完し、未知の非空IDと空配列は保持する。SchemaVersionは0以下だけをCurrentSchemaVersionへ補完し、現在値より大きな将来バージョンは保持する。

```csharp
internal static class UserSettingsJson
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public static UserSettings DeserializeOrDefault(string json)
    {
        try
        {
            return NormalizeStructure(
                JsonSerializer.Deserialize<UserSettings>(json, SerializerOptions) ?? new UserSettings());
        }
        catch (JsonException)
        {
            return new UserSettings();
        }
        catch (NotSupportedException)
        {
            return new UserSettings();
        }
    }

    public static string Serialize(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return JsonSerializer.Serialize(NormalizeStructure(settings), SerializerOptions);
    }

    private static UserSettings NormalizeStructure(UserSettings settings)
    {
        return new UserSettings
        {
            SchemaVersion = settings.SchemaVersion <= 0
                ? UserSettings.CurrentSchemaVersion
                : settings.SchemaVersion,
            DefaultHudId = string.IsNullOrWhiteSpace(settings.DefaultHudId)
                ? BuiltInHudIds.Music
                : settings.DefaultHudId,
            EnabledHudModuleIds = settings.EnabledHudModuleIds is null
                ? [BuiltInHudIds.Music]
                : [.. settings.EnabledHudModuleIds],
            Modules = settings.Modules is null
                ? new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                : settings.Modules.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.Clone(),
                    StringComparer.Ordinal),
            AdditionalProperties = settings.AdditionalProperties is null
                ? new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                : settings.AdditionalProperties.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.Clone(),
                    StringComparer.Ordinal),
            Variant = settings.Variant,
            ShowProgressBar = settings.ShowProgressBar,
            Language = settings.Language,
            ShowLyrics = settings.ShowLyrics,
            TextScrollMode = settings.TextScrollMode,
            HasCustomPosition = settings.HasCustomPosition,
            WindowLeft = settings.WindowLeft,
            WindowTop = settings.WindowTop,
            CheckUpdateOnStartup = settings.CheckUpdateOnStartup,
            DisableExpandOnFullscreen = settings.DisableExpandOnFullscreen
        };
    }
}
```

- [ ] **Step 5: ファイルI/OとMainViewModelの保存を互換化する**

`SettingsService.Load`／`Save` は `UserSettingsJson` を使い、旧ファイル移行と初回スタートアップ登録を維持する。`MainViewModel` は読み込んだ `UserSettings` を `private readonly UserSettings _settings;` に保持する。`SaveSettings` は新規インスタンスを作らず既知プロパティだけ更新し、未知ID、Modules、AdditionalPropertiesを保持する。Router用に `internal UserSettings SettingsSnapshot => _settings;` を公開する。

- [ ] **Step 6: 設定テストとReleaseビルドをGREENにする**

Run: `dotnet test NoraBar.Tests/NoraBar.Tests.csproj --filter FullyQualifiedName~UserSettingsJsonTests -c Release`

Expected: PASS、5 tests。

Run: `dotnet build NoraBar.slnx -c Release`

Expected: Build succeeded、今回追加した警告0件。

- [ ] **Step 7: 差分確認してコミットする**

```powershell
git status
git diff
git add NoraBar.slnx NoraBar/Hud/BuiltInHudIds.cs NoraBar/AssemblyInfo.cs NoraBar.Tests/NoraBar.Tests.csproj NoraBar.Tests/Settings/UserSettingsJsonTests.cs NoraBar/Services/UserSettingsJson.cs NoraBar/Services/SettingsService.cs NoraBar/ViewModels/MainViewModel.cs
git diff --staged
git commit -m "feat: バージョン付きHUD設定基盤を追加" -m "- NoraBar/Services
  - 未知HUD設定を保持する構造正規化を追加
- NoraBar/ViewModels/MainViewModel.cs
  - 読み込み済み設定を更新して未知項目を保持
- NoraBar.Tests
  - 旧設定と未知JSONの往復テストを追加
- NoraBar.slnx
  - テストプロジェクトを追加"
```

---

### Task 2: HUD契約とRegistry

**Files:**
- Create: `NoraBar/Hud/HudPresentationState.cs`
- Create: `NoraBar/Hud/HudRouterSnapshot.cs`
- Create: `NoraBar/Hud/HudSize.cs`
- Create: `NoraBar/Hud/HudViewContext.cs`
- Create: `NoraBar/Hud/HudModuleMetadata.cs`
- Create: `NoraBar/Hud/IHudModule.cs`
- Create: `NoraBar/Hud/HudRegistry.cs`
- Create: `NoraBar.Tests/Hud/FakeHudModule.cs`
- Create: `NoraBar.Tests/Hud/HudRegistryTests.cs`

**Interfaces:**
- Consumes: `BuiltInHudIds.Music` from Task 1
- Produces: `IHudModule : IAsyncDisposable`
- Produces: `HudRegistry.Register`, `TryGet`, `Modules`, `DisposeAsync`
- Produces: `FakeHudModule` for Task 3

- [ ] **Step 1: Registryの失敗テストとFakeHudModuleを書く**

`FakeHudModule` はID、呼出回数、順序ログ、Activate時例外、Activate中無効化通知を設定できるようにする。`GetView` は呼ばれた場合に `InvalidOperationException`、`GetPreferredSize` は `new HudSize(1, 1)` を返す。

```csharp
[Fact]
public void Register_RejectsDuplicateIdUsingOrdinalComparison()
{
    var registry = new HudRegistry();
    registry.Register(new FakeHudModule("music"));
    Assert.Throws<ArgumentException>(() => registry.Register(new FakeHudModule("music")));
    registry.Register(new FakeHudModule("Music"));
}

[Theory]
[InlineData("")]
[InlineData(" ")]
[InlineData(" music")]
[InlineData("music ")]
public void Register_RejectsInvalidIds(string id)
{
    var registry = new HudRegistry();
    Assert.Throws<ArgumentException>(() => registry.Register(new FakeHudModule(id)));
}
```

登録、Ordinal検索、登録順、破棄一回も独立テストにする。

- [ ] **Step 2: RegistryテストのREDを確認する**

Run: `dotnet test NoraBar.Tests/NoraBar.Tests.csproj --filter FullyQualifiedName~HudRegistryTests`

Expected: FAIL with missing `IHudModule`, `HudRegistry`, `HudSize`。

- [ ] **Step 3: 契約型を最小実装する**

```csharp
public enum HudPresentationState
{
    Collapsed,
    Peek,
    Expanded,
    Pinned
}

public readonly record struct HudRouterSnapshot(
    string? CurrentHudId,
    IHudModule? CurrentModule,
    HudPresentationState PresentationState,
    bool IsInitialized,
    bool IsShuttingDown);

public readonly record struct HudSize(double Width, double Height);
public readonly record struct HudViewContext(HudPresentationState PresentationState);
public sealed record HudModuleMetadata(string DisplayName, int DisplayOrder);

public interface IHudModule : IAsyncDisposable
{
    string Id { get; }
    HudModuleMetadata Metadata { get; }
    event EventHandler? PresentationInvalidated;
    ValueTask InitializeAsync(CancellationToken cancellationToken);
    ValueTask ActivateAsync(CancellationToken cancellationToken);
    ValueTask DeactivateAsync(CancellationToken cancellationToken);
    FrameworkElement GetView(HudViewContext context);
    HudSize GetPreferredSize(HudViewContext context);
}
```

公開契約には責務と引数の意味を説明するXMLドキュメントを付ける。

- [ ] **Step 4: Registryを実装する**

`HudRegistry` は `List<IHudModule>` と `Dictionary<string, IHudModule>(StringComparer.Ordinal)` を持つ。IDはnull／空白、前後空白、制御文字を拒否する。登録順を `IReadOnlyList<IHudModule> Modules` で返し、`DisposeAsync` はInterlockedで一度だけ全モジュールを登録順に破棄する。

- [ ] **Step 5: RegistryテストとReleaseビルドをGREENにする**

Run: `dotnet test NoraBar.Tests/NoraBar.Tests.csproj --filter FullyQualifiedName~HudRegistryTests -c Release`

Expected: PASS。

Run: `dotnet build NoraBar.slnx -c Release`

Expected: Build succeeded。

- [ ] **Step 6: 差分確認してコミットする**

```powershell
git status
git diff
git add NoraBar/Hud NoraBar.Tests/Hud/FakeHudModule.cs NoraBar.Tests/Hud/HudRegistryTests.cs
git diff --staged
git commit -m "feat: HUDモジュール契約とレジストリを追加" -m "- NoraBar/Hud
  - HUD ID、表示状態、サイズ、共通モジュール契約を追加
  - Ordinal検索と冪等な破棄を行うRegistryを追加
- NoraBar.Tests/Hud
  - FakeHudModuleとRegistryテストを追加"
```

---

### Task 3: 直列化されたHudRouterとライフサイクル

**Files:**
- Create: `NoraBar/Hud/HudNavigationException.cs`
- Create: `NoraBar/Hud/HudRouter.cs`
- Create: `NoraBar.Tests/Hud/HudRouterTests.cs`
- Modify: `NoraBar.Tests/Hud/FakeHudModule.cs`

**Interfaces:**
- Consumes: `HudRegistry`, `IHudModule`, `BuiltInHudIds.Music`
- Produces: `InitializeAsync`, `NavigateToAsync`, `DisableAsync`, `ShutdownAsync`
- Produces: `CurrentModule`, `CurrentHudId`, `PresentationState`, `EffectiveDefaultHudId`, `EnabledHudModuleIds`
- Produces: `GetSnapshot(): HudRouterSnapshot`
- Produces: `StateChanged`, `PresentationChanged`

- [ ] **Step 1: 初期状態と表示状態の失敗テストを書く**

```csharp
[Fact]
public async Task InitializeAsync_SelectsMusicCollapsedAndActivatesOnce()
{
    var music = new FakeHudModule(BuiltInHudIds.Music);
    var registry = new HudRegistry();
    registry.Register(music);
    var router = new HudRouter(registry, BuiltInHudIds.Music, [BuiltInHudIds.Music]);
    await router.InitializeAsync(CancellationToken.None);
    await router.InitializeAsync(CancellationToken.None);
    Assert.Same(music, router.CurrentModule);
    Assert.Equal(BuiltInHudIds.Music, router.CurrentHudId);
    Assert.Equal(HudPresentationState.Collapsed, router.PresentationState);
    Assert.Equal(1, music.InitializeCount);
    Assert.Equal(1, music.ActivateCount);
}

[Fact]
public async Task PresentationChanges_DoNotChangeModuleLifecycle()
{
    FakeHudModule music = CreateMusic();
    HudRouter router = await CreateInitializedRouterAsync(music);
    router.SetPresentationState(HudPresentationState.Expanded);
    router.SetPresentationState(HudPresentationState.Pinned);
    bool collapsed = router.CollapseFromPointerLeave();
    Assert.False(collapsed);
    Assert.Equal(HudPresentationState.Pinned, router.PresentationState);
    Assert.Equal(1, music.ActivateCount);
    Assert.Equal(0, music.DeactivateCount);
}

[Fact]
public async Task GetSnapshot_ReturnsOneConsistentInitializedState()
{
    FakeHudModule music = CreateMusic();
    HudRouter router = await CreateInitializedRouterAsync(music);
    HudRouterSnapshot snapshot = router.GetSnapshot();
    Assert.True(snapshot.IsInitialized);
    Assert.False(snapshot.IsShuttingDown);
    Assert.Equal(BuiltInHudIds.Music, snapshot.CurrentHudId);
    Assert.Same(music, snapshot.CurrentModule);
    Assert.Equal(HudPresentationState.Collapsed, snapshot.PresentationState);
}
```

不明HUD、無効HUD、空の実行時有効一覧、StateChanged回数も別テストで追加する。

- [ ] **Step 2: 初期状態テストのREDを確認する**

Run: `dotnet test NoraBar.Tests/NoraBar.Tests.csproj --filter FullyQualifiedName~HudRouterTests`

Expected: FAIL with missing `HudRouter`。

- [ ] **Step 3: Routerの状態と実行時フォールバックを実装する**

`HudRouter(HudRegistry registry, string configuredDefaultHudId, IEnumerable<string> configuredEnabledHudModuleIds)` は設定値をコピーし、保存元を変更しない。Registry登録済みかつ設定で有効なIDだけを実行時一覧へ入れる。一件もなければ登録済み `music`、次に登録順先頭を実行時だけ補完する。EffectiveDefaultも設定既定、`music`、登録順先頭の順で解決する。

`InitializeAsync` は `SemaphoreSlim` で直列化し、既定モジュールのInitialize、購読、Activate、Current確定、Collapsed確定、StateChangedの順で一度だけ実行する。

CurrentHudId、CurrentModule、PresentationState、初期化／終了フラグは `private readonly object _stateLock` で保護する。`GetSnapshot` は一回のlock内で全フィールドを読み、不変な `HudRouterSnapshot` を返す。初期化前とShutdown後はCurrentHudId／CurrentModuleをnullにし、IsInitialized／IsShuttingDownで状態を明示する。

- [ ] **Step 4: 遷移順序と通知保留の失敗テストを書く**

```csharp
[Fact]
public async Task NavigateToAsync_UsesRequiredLifecycleAndNotificationOrder()
{
    var calls = new List<string>();
    var music = new FakeHudModule("music", calls);
    var launcher = new FakeHudModule("launcher", calls) { InvalidateDuringActivate = true };
    HudRouter router = await CreateInitializedRouterAsync(music, launcher);
    calls.Clear();
    router.StateChanged += (_, _) => calls.Add("router:state");
    router.PresentationChanged += (_, _) => calls.Add("router:presentation");
    await router.NavigateToAsync("launcher", CancellationToken.None);
    Assert.Equal(
        new[]
        {
            "music:unsubscribe",
            "music:deactivate",
            "launcher:initialize",
            "launcher:subscribe",
            "launcher:activate",
            "launcher:invalidate",
            "router:state",
            "router:presentation"
        },
        calls);
}
```

FakeHudModuleのイベントadd/removeでsubscribe/unsubscribeを記録する。Activate中に複数回通知しても遷移後のPresentationChangedが一回であることを追加検証する。

- [ ] **Step 5: 失敗復旧、無効化、直列化の失敗テストを書く**

次を個別テストにする。

- Activate例外時、新モジュールを購読解除・Deactivateし、旧モジュールを再ActivateしてCurrentを維持する。
- 旧HUD復旧も失敗した場合、実行時既定HUDへ復旧する。
- 復旧確定前にStateChangedを通知しない。
- 現在HUDを無効化する前にフォールバックをActivateする。
- 最後の有効HUDを無効化すると実行時だけ `music` を補完する。
- 同時Navigateを `TaskCompletionSource` で停止し、呼出が交差しない。
- Shutdownは購読解除後にDeactivateし、二回呼んでも一回だけ実行する。
- Shutdown開始後のSetPresentationStateとCollapseFromPointerLeaveはfalseを返し、状態も通知回数も変えない。
- HUD切り替え中のSetPresentationStateとCollapseFromPointerLeaveはfalseを返し、Current確定前の状態通知を発行しない。
- 同じPresentationStateへの設定はfalseを返し、StateChangedを発行しない。
- StateChanged／PresentationChangedハンドラーからGetSnapshotを呼んでもデッドロックしない。

- [ ] **Step 6: 遷移、復旧、停止を実装する**

`NavigateToAsync`、`DisableAsync`、`ShutdownAsync` は同じSemaphoreSlimを使う。`SetPresentationState` と `CollapseFromPointerLeave` は同期APIのままboolを返し、短い状態lock内で初期化済み・遷移中でない・終了中でない・値が変わることを検証して更新する。StateChangedは必ずlockを解放してから発行する。`OnModulePresentationInvalidated` は遷移中なら保留フラグだけを立て、終了中は無視し、それ以外はlock外でPresentationChangedを通知する。

成功順序は旧購読解除、旧Deactivate、新Initialize、新購読、遷移中設定、新Activate、Current確定、遷移中解除、StateChanged、保留PresentationChangedとする。

Activate失敗時は新購読解除、新Deactivateを試行し、旧モジュール、実行時既定モジュールの順に復旧する。復旧後も元の例外を `HudNavigationException` として返し、Currentは復旧先を指す。復旧失敗も例外情報へ含める。

- [ ] **Step 7: RouterテストとReleaseビルドをGREENにする**

Run: `dotnet test NoraBar.Tests/NoraBar.Tests.csproj --filter FullyQualifiedName~HudRouterTests -c Release`

Expected: PASS。

Run: `dotnet build NoraBar.slnx -c Release`

Expected: Build succeeded。

- [ ] **Step 8: 差分確認してコミットする**

```powershell
git status
git diff
git add NoraBar/Hud/HudNavigationException.cs NoraBar/Hud/HudRouter.cs NoraBar.Tests/Hud/FakeHudModule.cs NoraBar.Tests/Hud/HudRouterTests.cs
git diff --staged
git commit -m "feat: HUDルーターとライフサイクルを追加" -m "- NoraBar/Hud
  - 遷移直列化、実行時フォールバック、通知保留を追加
  - 冪等な初期化、切り替え、無効化、停止を追加
- NoraBar.Tests/Hud
  - 遷移順序、失敗復旧、Pinned、並行遷移のテストを追加"
```

---

### Task 4: 音楽HUDモジュール

**Files:**
- Create: `NoraBar/Hud/Music/IMusicHudPresentationSource.cs`
- Create: `NoraBar/Hud/Music/MainViewModelMusicHudPresentationSource.cs`
- Create: `NoraBar/Hud/Music/MusicHudLayout.cs`
- Create: `NoraBar/Hud/Music/MusicHudModule.cs`
- Create: `NoraBar.Tests/Hud/MusicHudLayoutTests.cs`
- Create: `NoraBar.Tests/Hud/MusicHudModuleTests.cs`

**Interfaces:**
- Consumes: `IHudModule`、`MainViewModel`、3つの既存音楽View
- Produces: `MusicHudModule` with ID `music`
- Produces: デザイン別Viewキャッシュと純粋サイズ計算

- [ ] **Step 1: サイズ計算の失敗テストを書く**

Minimalは幅450、高さ80/106に歌詞24と複数セッション12を加算する。Productivityは幅560、高さ90/120に歌詞24と複数セッション16を加算する。Lyrics Focusは常に650x180とする。

```csharp
[Theory]
[InlineData(false, false, false, 450, 80)]
[InlineData(true, true, true, 450, 142)]
public void Calculate_MinimalMatchesExistingLayout(
    bool progress, bool lyrics, bool multiple, double width, double height)
{
    HudSize result = MusicHudLayout.Calculate(
        DesignVariant.MinimalFloatingPill, progress, lyrics, multiple);
    Assert.Equal(new HudSize(width, height), result);
}

[Theory]
[InlineData(false, false, false, 560, 90)]
[InlineData(true, true, true, 560, 160)]
public void Calculate_ProductivityMatchesExistingLayout(
    bool progress, bool lyrics, bool multiple, double width, double height)
{
    HudSize result = MusicHudLayout.Calculate(
        DesignVariant.ProductivityCommandIsland, progress, lyrics, multiple);
    Assert.Equal(new HudSize(width, height), result);
}

[Fact]
public void Calculate_LyricsFocusAlwaysUsesFixedSize()
{
    HudSize result = MusicHudLayout.Calculate(
        DesignVariant.LyricsFocusedSidebar,
        showProgressBar: false,
        showLyrics: false,
        hasMultipleSessions: false);
    Assert.Equal(new HudSize(650, 180), result);
}
```

- [ ] **Step 2: サイズテストのREDを確認する**

Run: `dotnet test NoraBar.Tests/NoraBar.Tests.csproj --filter FullyQualifiedName~MusicHudLayoutTests`

Expected: FAIL with missing `MusicHudLayout`。

- [ ] **Step 3: 純粋サイズ計算を実装してGREENにする**

サイズ値は意味のある `const double` として `MusicHudLayout` に集約し、switch式で3デザインを処理する。未知enum値は `ArgumentOutOfRangeException` にする。

Run: `dotnet test NoraBar.Tests/NoraBar.Tests.csproj --filter FullyQualifiedName~MusicHudLayoutTests -c Release`

Expected: PASS。

- [ ] **Step 4: Viewキャッシュ、無効化、Cleanupの失敗テストを書く**

`IMusicHudPresentationSource` のFakeをテスト内に作り、CurrentVariant、ShowProgressBar、ShowLyrics、HasMultipleSessions、ShellPropertyChanged、MusicPropertyChanged、CleanupCountを制御する。次を個別テストにする。

- 同じデザインのGetViewは同じViewを返す。
- 3デザインはそれぞれ一度だけ生成される。
- ViewのDataContextは互換コンテキストになる。
- Initializeを二回呼んでもイベント購読は一回。
- Variant、ShowProgressBar、ShowLyrics、HasMultipleSessionsだけがPresentationInvalidatedを発生させる。
- Disposeを二回呼んでも購読解除とCleanupは一回。
- Collapsed／Expanded変更だけではモジュール側ライフサイクル回数が変わらない。

- [ ] **Step 5: 音楽表示ソースとMusicHudModuleを実装する**

```csharp
internal interface IMusicHudPresentationSource
{
    DesignVariant CurrentVariant { get; }
    bool ShowProgressBar { get; }
    bool ShowLyrics { get; }
    bool HasMultipleSessions { get; }
    object ViewDataContext { get; }
    event PropertyChangedEventHandler? ShellPropertyChanged;
    event PropertyChangedEventHandler? MusicPropertyChanged;
    void Cleanup();
}
```

`MainViewModelMusicHudPresentationSource` はMainViewModelとMusicViewModelのイベントをadd/removeアクセサーで転送し、Cleanupを `MainViewModel.Music.Cleanup` へ委譲する。

`MusicHudModule` は本番コンストラクターでアダプターと3Viewのfactoryを構成し、内部コンストラクターでFake source/factoryを受ける。`Dictionary<DesignVariant, FrameworkElement>` でViewをキャッシュし、生成時に `DataContext = source.ViewDataContext` を設定する。

Initialize／Activate／Deactivate／DisposeはInterlockedまたはロックで冪等にする。Initializeでイベント購読、Disposeで購読解除後にsource.Cleanupを一度だけ呼ぶ。MainViewModel側はCurrentVariant、ShowProgressBar、ShowLyrics、Music側はHasMultipleSessionsだけをPresentationInvalidatedへ変換する。

- [ ] **Step 6: 音楽モジュールテストとReleaseビルドをGREENにする**

Run: `dotnet test NoraBar.Tests/NoraBar.Tests.csproj --filter "FullyQualifiedName~MusicHudLayoutTests|FullyQualifiedName~MusicHudModuleTests" -c Release`

Expected: PASS。

Run: `dotnet build NoraBar.slnx -c Release`

Expected: Build succeeded。

- [ ] **Step 7: 差分確認してコミットする**

```powershell
git status
git diff
git add NoraBar/Hud/Music NoraBar.Tests/Hud/MusicHudLayoutTests.cs NoraBar.Tests/Hud/MusicHudModuleTests.cs
git diff --staged
git commit -m "refactor: 音楽HUDを組み込みモジュールへ移行" -m "- NoraBar/Hud/Music
  - 3デザインのViewキャッシュとサイズ計算を追加
  - 音楽表示変更の汎用無効化通知を追加
  - Cleanup所有をMusicHudModuleへ集約
- NoraBar.Tests/Hud
  - サイズ、キャッシュ、購読、破棄のテストを追加"
```

---

### Task 5: App Composition Rootと汎用MainWindow

**Files:**
- Create: `NoraBar/Services/ShutdownTaskCoordinator.cs`
- Create: `NoraBar.Tests/Services/ShutdownTaskCoordinatorTests.cs`
- Create: `NoraBar.Tests/Architecture/MainWindowDependencyTests.cs`
- Modify: `NoraBar/App.xaml`
- Modify: `NoraBar/App.xaml.cs`
- Modify: `NoraBar/MainWindow.xaml.cs`
- Modify: `NoraBar/ViewModels/MainViewModel.cs`

**Interfaces:**
- Consumes: `MusicHudModule`、`HudRegistry`、`HudRouter`
- Produces: `App.RequestShutdownAsync(): Task`
- Produces: `MainWindow(MainViewModel, HudRouter, Func<Task>)`
- Removes: MainWindowの音楽型・音楽設定・`IslandState` 依存

- [ ] **Step 1: 終了要求統合の失敗テストを書く**

```csharp
[Fact]
public async Task RunOnce_ReturnsSameTaskAndExecutesOperationOnce()
{
    var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var coordinator = new ShutdownTaskCoordinator();
    int calls = 0;
    Task first = coordinator.RunOnce(() =>
    {
        Interlocked.Increment(ref calls);
        return completion.Task;
    });
    Task second = coordinator.RunOnce(() => throw new InvalidOperationException());
    Assert.Same(first, second);
    Assert.Equal(1, calls);
    completion.SetResult();
    await first;
}
```

- [ ] **Step 2: REDを確認してShutdownTaskCoordinatorを実装する**

Run: `dotnet test NoraBar.Tests/NoraBar.Tests.csproj --filter FullyQualifiedName~ShutdownTaskCoordinatorTests`

Expected: FAIL with missing type。

`RunOnce(Func<Task>)` はlock内で `_task ??= operation()` を返す。null delegateは拒否し、同期例外は `Task.FromException` として同じTaskに保持する。

Run: `dotnet test NoraBar.Tests/NoraBar.Tests.csproj --filter FullyQualifiedName~ShutdownTaskCoordinatorTests -c Release`

Expected: PASS。

- [ ] **Step 3: MainWindow構造テストのREDを書く**

```csharp
[Fact]
public void MainWindow_DependsOnRouterButDoesNotStoreHudModule()
{
    Type type = typeof(MainWindow);
    ConstructorInfo constructor = Assert.Single(type.GetConstructors());
    Type[] parameters = constructor.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
    Assert.Contains(typeof(HudRouter), parameters);
    Assert.DoesNotContain(parameters, parameter => typeof(IHudModule).IsAssignableFrom(parameter));
    Assert.DoesNotContain(
        type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic),
        field => typeof(IHudModule).IsAssignableFrom(field.FieldType));
}
```

- [ ] **Step 4: App.xamlを明示起動へ変更する**

`StartupUri="MainWindow.xaml"` を削除し、Applicationへ `ShutdownMode="OnExplicitShutdown"` を設定する。`App.OnStartup` はWPFイベント境界として `async void` を使用する。

```csharp
MainViewModel viewModel = new();
MusicHudModule musicModule = new(viewModel);
HudRegistry registry = new();
registry.Register(musicModule);
UserSettings settings = viewModel.SettingsSnapshot;
HudRouter router = new(registry, settings.DefaultHudId, settings.EnabledHudModuleIds);
MainWindow window = new(viewModel, router, RequestShutdownAsync);
MainWindow = window;
await router.InitializeAsync(CancellationToken.None);
window.RefreshHudPresentation();
window.Show();
```

実際には各オブジェクトをAppのprivate fieldへ保存する。起動失敗時はMessageBoxで示し、同期資源を解放後に `Shutdown(-1)` する。既存Mutex、起動引数、設定画面表示、更新確認を維持する。

- [ ] **Step 5: MainWindowをRouter通知だけの汎用ホストへ変更する**

コンストラクターはMainViewModel、HudRouter、`Func<Task> requestShutdownAsync` を受け取り、Router初期化前にStateChangedとPresentationChangedを購読する。両イベントはDispatcher上で `RefreshHudPresentation` を呼ぶ。

`RefreshHudPresentation` は `HudRouterSnapshot snapshot = router.GetSnapshot();` を一度だけ実行する。初期化済みかつCurrentModuleが非nullの場合だけ、そのスナップショットのCurrentModuleとPresentationStateから `HudViewContext` を作り、GetViewとGetPreferredSizeを問い合わせる。同じ更新内でRouterプロパティを再読込しない。Collapsedなら共通定数200x2でコンテンツを閉じ、その他では全画面抑制を確認してViewを表示する。PinnedのMouseLeaveは `router.CollapseFromPointerLeave()` に委譲する。

位置、DPI、設定画面、トレイ、言語、全画面、アニメーションは維持する。`MainViewModel.CurrentState` と `SetStateCommand` は参照がないことをrgで確認してMainViewModelから削除する。`IslandState.cs` は破壊的削除せず未参照のまま残す。

- [ ] **Step 6: App統括の終了フローを実装する**

MainWindowの終了メニューは重複アニメーションを防ぎ、アニメーション完了イベントから `await requestShutdownAsync()` を呼ぶ。`App.RequestShutdownAsync` は `ShutdownTaskCoordinator.RunOnce(ShutdownCoreAsync)` を返す。`ShutdownCoreAsync` は次の順にする。

1. `MainWindow.DetachHudRouter()`
2. `await HudRouter.ShutdownAsync(CancellationToken.None)`
3. `await HudRegistry.DisposeAsync()`
4. `MainWindow.ReleaseShellResources()`
5. `MainWindow.AllowClose()`
6. `MainWindow.Close()`
7. `Application.Shutdown()`

MainWindow.OnClosedとApp.OnExitは新しい非同期終了を開始しない。MusicViewModel.Cleanupの直接呼出をMainWindowから削除し、App/Routerにも追加しない。

MainWindowは `_allowClose` と `_shutdownRequested` で全Close経路を統合する。`OnClosing` はAppの最終Close以外を一度キャンセルし、終了アニメーションを一度だけ開始して共通のrequestShutdownAsyncへ転送する。Appが `AllowClose()` を呼んだ後の `Close()` だけはキャンセルしない。

```csharp
protected override void OnClosing(CancelEventArgs e)
{
    if (!_allowClose)
    {
        e.Cancel = true;
        RequestShutdownFromWindow();
        return;
    }
    base.OnClosing(e);
}
```

`RequestShutdownFromWindow` は終了メニュー、トレイ、Alt+F4、システムメニュー、OS Closeから共有し、Interlockedまたは状態lockでアニメーションと終了Taskの重複起動を防ぐ。

- [ ] **Step 7: 構造テストと禁止依存チェックをGREENにする**

Run: `dotnet test NoraBar.Tests/NoraBar.Tests.csproj --filter "FullyQualifiedName~ShutdownTaskCoordinatorTests|FullyQualifiedName~MainWindowDependencyTests" -c Release`

Expected: PASS。

Run:

```powershell
rg -n "DesignAMusicView|DesignBMusicView|DesignCMusicView|DesignVariant|ShowLyrics|ShowProgressBar|HasMultipleSessions|PresentationInvalidated|IslandState|Music\.Cleanup" NoraBar/MainWindow.xaml.cs
rg -n "Music\.Cleanup" NoraBar/App.xaml.cs NoraBar/Hud/HudRouter.cs NoraBar/MainWindow.xaml.cs
rg -n "OnClosing|RequestShutdownFromWindow|AllowClose|GetSnapshot" NoraBar/MainWindow.xaml.cs
```

Expected: 禁止依存とCleanup検索はno matches。終了経路とSnapshot検索は4識別子すべてにmatch。

- [ ] **Step 8: 全関連テストとReleaseビルドを実行する**

Run: `dotnet test NoraBar.Tests/NoraBar.Tests.csproj -c Release`

Expected: PASS、失敗0件。

Run: `dotnet build NoraBar.slnx -c Release`

Expected: Build succeeded、今回追加した警告0件。

- [ ] **Step 9: 差分確認してコミットする**

```powershell
git status
git diff
git add NoraBar/App.xaml NoraBar/App.xaml.cs NoraBar/MainWindow.xaml.cs NoraBar/ViewModels/MainViewModel.cs NoraBar/Services/ShutdownTaskCoordinator.cs NoraBar.Tests/Services/ShutdownTaskCoordinatorTests.cs NoraBar.Tests/Architecture/MainWindowDependencyTests.cs
git diff --staged
git commit -m "refactor: MainWindowを汎用HUDホストへ移行" -m "- NoraBar/App.xaml
  - StartupUriを削除して明示終了へ変更
- NoraBar/App.xaml.cs
  - Composition Rootと共通非同期終了を追加
- NoraBar/MainWindow.xaml.cs
  - Router通知だけを監視する汎用HUDホストへ移行
- NoraBar/ViewModels/MainViewModel.cs
  - HUD表示状態の旧管理を除去
- NoraBar/Services/ShutdownTaskCoordinator.cs
  - 重複終了要求を同じTaskへ統合
- NoraBar.Tests
  - 終了統合とMainWindow依存境界のテストを追加"
```

---

### Task 6: アーキテクチャ文書

**Files:**
- Modify: `docs/wiki/アーキテクチャ.md`
- Modify: `docs/wiki/Architecture.md`

**Interfaces:**
- Documents: モジュール契約、Registry、Router、状態分離、MainWindow、ライフサイクル、設定、追加手順、外部プラグイン境界

- [ ] **Step 1: 日本語文書を更新する**

全体構成、HUD IDと表示状態、IHudModule、HudRegistry、HudRouter、MusicHudModule、MainWindow、起動・切り替え・終了ライフサイクル、設定スキーマと移行、新しい組み込みHUDの追加、Phase 0の外部プラグイン境界を見出しとして記載する。

最小追加例には `IHudModule` 実装、`registry.Register(new ClockHudModule())`、設定での有効化までを示す。外部DLLロードは未実装と明記する。

- [ ] **Step 2: 英語文書を同じ構造で更新する**

日本語版と同じ責務、順序、制限を英語で記載する。日本語版だけに存在する設計事項を残さない。

- [ ] **Step 3: 文書と実装の識別子を照合する**

Run:

```powershell
rg -n "IHudModule|HudRegistry|HudRouter|HudPresentationState|MusicHudModule|SchemaVersion|EnabledHudModuleIds|external plug-in|外部プラグイン" docs/wiki/アーキテクチャ.md docs/wiki/Architecture.md
```

Expected: 両文書に全主要概念が存在する。

- [ ] **Step 4: 差分確認してコミットする**

```powershell
git status
git diff
git add docs/wiki/アーキテクチャ.md docs/wiki/Architecture.md
git diff --staged
git commit -m "docs: モジュラーHUDアーキテクチャを文書化" -m "- docs/wiki/アーキテクチャ.md
  - HUD基盤、ライフサイクル、設定移行、追加手順を記載
- docs/wiki/Architecture.md
  - 日本語版と同じモジュラーHUD設計を英語で記載"
```

---

### Task 7: 全体検証と最終レビュー

**Files:**
- Review: 変更されたすべてのコード、テスト、ドキュメント
- Modify: 検証で判明した今回変更由来の問題があるファイルだけ

**Interfaces:**
- Verifies: Phase 0の成功条件20項目

- [ ] **Step 1: 不要コードと禁止パターンを確認する**

Run:

```powershell
rg -n "TODO|FIXME|Console\.Write|Debug\.Write|\.Result\b|\.Wait\(" NoraBar NoraBar.Tests
rg -n "DesignAMusicView|DesignBMusicView|DesignCMusicView|DesignVariant|ShowLyrics|ShowProgressBar|HasMultipleSessions|PresentationInvalidated|IslandState|Music\.Cleanup" NoraBar/MainWindow.xaml.cs
rg -n "Music\.Cleanup" NoraBar/App.xaml.cs NoraBar/Hud/HudRouter.cs NoraBar/MainWindow.xaml.cs
```

Expected: 今回追加したTODO、デバッグ出力、同期ブロックなし。MainWindow禁止依存とCleanup直接呼出なし。

- [ ] **Step 2: restoreを実行する**

Run: `dotnet restore NoraBar.slnx`

Expected: Restore succeeded。

- [ ] **Step 3: Releaseビルドを実行する**

Run: `dotnet build NoraBar.slnx -c Release --no-restore`

Expected: Build succeeded、今回変更由来の警告0件。

- [ ] **Step 4: Releaseテストを実行する**

Run: `dotnet test NoraBar.slnx -c Release --no-build`

Expected: 全テストPASS、失敗0件。

- [ ] **Step 5: Debugビルドを実行する**

Run: `dotnet build NoraBar.slnx -c Debug --no-restore`

Expected: Build succeeded。

- [ ] **Step 6: npm/react-doctor対象外を確認する**

Run: `Test-Path package.json`

Expected: `False`。npm lint/testとreact-doctorはWPF/.NETリポジトリのためNOT APPLICABLEとして最終報告する。

- [ ] **Step 7: GUI手動確認の可否を判定する**

GUIを操作できる場合だけアプリを起動し、ホバー展開、マウス離脱、3デザイン、歌詞、プログレスバー、音楽操作、全画面抑制、位置編集、設定再起動、旧設定読込を確認する。操作できない場合は `manual smoke test: NOT RUN` とし、実行したと報告しない。

- [ ] **Step 8: コードレビューを実施する**

`code-review` スキルで `v1.1.0..HEAD` をレビューする。P0/P1/P2の指摘を修正し、修正したテストを再実行する。仕様適合、例外復旧、購読解除、冪等性、未知設定保持、MainWindow禁止依存を重点確認する。

- [ ] **Step 9: 最終差分とコミット履歴を確認する**

Run:

```powershell
git status --short --branch
git diff --check v1.1.0..HEAD
git log --oneline --decorate v1.1.0..HEAD
```

Expected: 作業ツリーclean、diff checkエラーなし、意味のある作業単位のローカルコミットのみ。

- [ ] **Step 10: 検証修正がある場合だけコミットする**

検証修正がある場合は、修正したファイルだけをステージする。コミット件名は `fix: Phase 0検証で判明した問題を修正` とし、本文にはステージした各ファイルパスと、そのファイルで修正した具体的な不具合を記載する。修正がなければコミットしない。

---

## Completion Report

最終回答では、実装概要、主な変更ファイル、互換性、設定移行、実行した全コマンドとPASS/FAIL、成功条件20項目、残るPhase 0制限、base/working branch、初期作業ツリー、全コミットSHAとメッセージ、push/PR/merge未実施を報告する。

コミットメッセージ案は日本語のConventional Commits形式で、ファイル単位の詳細を含める。
