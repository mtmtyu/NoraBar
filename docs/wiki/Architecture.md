# Architecture

NoraBar is based on WPF MVVM. Phase 0 modularizes HUD selection and presentation: `App` is the composition root, `HudRouter` owns selection lifecycle, and `MainWindow` is a generic shell. The existing `MainViewModel` remains the compatibility DataContext for music views.

## Overall Structure

```text
App (composition root)
  ├─ MainViewModel → MusicViewModel → MediaControl / AudioVisualizer / Lyrics
  ├─ MusicHudModule : IHudModule
  ├─ HomeHudModule : IHudModule
  ├─ HudRegistry
  ├─ HudRouter
  ├─ HudNavigationViewModel
  └─ MainWindow
       ├─ HudRouter.StateChanged / PresentationChanged
       ├─ hosts the current module's view
       └─ navigation, position, DPI, input, animation, settings, tray, shutdown
```

`App.xaml` has no `StartupUri` and specifies `ShutdownMode="OnExplicitShutdown"`. `App.OnStartup` explicitly constructs dependencies and connects them with constructor injection. No DI container is used.

## HUD IDs and PresentationState

HUDs use stable string IDs. Built-in IDs are centralized in `BuiltInHudIds`; Music is `music` and Home is `home`. `HudRegistry` and Router use case-sensitive ordinal ID comparison.

`HudPresentationState` is independent from HUD selection and contains `Collapsed`, `Peek`, `Expanded`, and `Pinned`. `SetPresentationState` and `CollapseFromPointerLeave` change presentation only; they do not call `ActivateAsync` or `DeactivateAsync`. Requests are rejected without notification when the state is unchanged, before initialization, during a transition, or after shutdown starts. `Pinned` does not collapse on pointer leave.

## IHudModule

`IHudModule` is the WPF-facing contract for HUD identity, selection lifecycle, views, and preferred size.

```csharp
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

`InitializeAsync` performs first-use preparation, `ActivateAsync` selects the current HUD, and `DeactivateAsync` runs during switching or shutdown. A module raises `PresentationInvalidated` when state affects its view or size. `HudViewContext` carries presentation state, while `HudSize` returns preferred width and height. Lifecycle methods and `DisposeAsync` should be idempotent wherever practical.

## HudRegistry

`HudRegistry` manually registers `IHudModule` instances and provides ordinal ID lookup, duplicate and invalid-ID rejection, registration-order enumeration, and exactly-once asynchronous disposal of all modules. It synchronizes registration with disposal start and aggregates multiple disposal failures. It does not scan assemblies or load external DLLs.

## HudRouter

`HudRouter` owns runtime enabled HUDs, the effective default, current HUD, presentation state, and selection lifecycle. Router is the sole owner of module `PresentationInvalidated` subscriptions and converts them to generic `PresentationChanged` events.

### Consistent Snapshot

`GetSnapshot()` reads related values under one short state lock and returns one immutable value.

```csharp
public readonly record struct HudRouterSnapshot(
    string? CurrentHudId,
    IHudModule? CurrentModule,
    HudPresentationState PresentationState,
    bool IsInitialized,
    bool IsShuttingDown);
```

The ID and module are `null` before initialization and after shutdown. For each notification, `MainWindow` obtains one snapshot, creates a `HudViewContext` from it, and asks the same module for `GetView` and `GetPreferredSize`.

### Synchronization and Notifications

`InitializeAsync`, `NavigateToAsync`, `ApplyConfigurationAsync`, `DisableAsync`, and `ShutdownAsync` are serialized by a `SemaphoreSlim`. State reads and writes use a short `lock`; `StateChanged` and `PresentationChanged` are raised outside that lock. The synchronous presentation APIs use the same lock to prevent competition with transitions and shutdown. `ApplyConfigurationAsync` applies enabled order and startup HUD immediately and moves to a valid fallback before disabling the current module.

Invalidations raised during `ActivateAsync` are deferred. After the current HUD is committed and `StateChanged` is raised, any number of deferred invalidations are coalesced into one `PresentationChanged`. Unregistered settings IDs are excluded from the runtime enabled list. If it becomes empty, Router adds `music`, or the first registered module if music is unavailable. This runtime correction does not rewrite persisted settings.

## MusicHudModule

`MusicHudModule` is the first built-in module. It owns Minimal, Productivity, and Lyrics Focus view selection and per-design caching, the compatibility DataContext, preferred-size calculation from `ShowProgressBar`, `ShowLyrics`, and `HasMultipleSessions`, presentation observation, WPF Dispatcher consistency, and Cleanup.

It reevaluates cached views and preferred size instead of rebuilding views for every settings change. `InitializeAsync` establishes subscriptions once. `DisposeAsync` removes them, waits for in-flight notifications, and runs Cleanup exactly once. `MusicHudModule.DisposeAsync` is the only direct owner of `MusicViewModel.Cleanup`.

## HomeHudModule

`HomeHudModule` is the built-in overview HUD. It owns one cached `DynamicWidgetHomeView`; preferred size is derived from the active widget list and configured widget viewport. `HomeHudViewModel` adapts the existing music state and adds a local clock plus up to three configurable world clocks. Its one-second clock timer runs only while Home is active, and presentation invalidation is forwarded through Router. Module and preview disposal also dispose their owned Home view trees. The cached WPF view is disposed on its owning Dispatcher before WPF shutdown; cross-thread disposal is bounded, and an unavailable Dispatcher triggers thread-safe managed-subscription cleanup plus a reported cleanup failure instead of wrong-thread visual-tree access. If a timed-out Dispatcher cleanup has already started, that callback retains exclusive ownership of View cleanup and source disposal is deferred until it finishes. The module exposes that final owned cleanup as one stable task, reports late failures, and application shutdown waits for it with a separate bound. Dynamic widget rebuilds dispose removed widget views before clearing the visual tree, and a failed Widget disposal still triggers managed-subscription release before ownership is cleared.

Home settings live in the `home` module payload: the widget list, viewport width and height, system/12-hour/24-hour time format, and the label and Windows time-zone ID for each world clock. The legacy `DesignVariant` value is preserved for settings compatibility but no longer selects a View. With no media session, the music region keeps its layout, shows the localized no-media state, and disables transport controls. The media widget subscribes to music changes only while loaded, detaches on unload or disposal, and cancels bounded lyric-scroll work when it leaves the visual tree. The settings window creates a separate disposable presentation source for live preview, so preview lifetime does not alter the active module. If preview hosting fails after assigning content, the session clears the host and ownership before disposing the failed preview while preserving the hosting failure.

## MainWindow

For HUD view selection and preferred size, `MainWindow` is a generic host that observes only `StateChanged` and `PresentationChanged`. It does not subscribe directly to modules and reevaluates view and size from one Router snapshot. It continues to observe shell-specific `MainViewModel` state such as language, navigation placement, and position editing.

It owns top-edge and multi-monitor positioning, DPI, input, the common collapsed `200x2` size, animation, fullscreen suppression, position editing, language, settings, tray, and shutdown requests. While expanded or pinned, it adds generic navigation chrome as either a right icon rail or top icon-and-name tabs. Click and wheel navigation are handled only inside this chrome, preserving module-owned scrolling such as music-session selection. It has no module-specific view types or sizing branches.

Alt+F4, the system menu, and OS Close also reach `OnClosing`. Unless App has authorized the final close, MainWindow cancels it and forwards it through the guarded exit animation and common shutdown flow.

## Startup Lifecycle

```text
App.OnStartup
  → MainViewModel → MusicHudModule + HomeHudModule → register with HudRegistry → HudRouter
  → HudNavigationViewModel → attach to MainViewModel
  → construct MainWindow and subscribe to StateChanged / PresentationChanged
  → HudRouter.InitializeAsync
       → resolve effective default
       → InitializeAsync (first time only)
       → subscribe to PresentationInvalidated
       → ActivateAsync
       → commit CurrentHudId / CurrentModule / Collapsed
       → StateChanged → deferred PresentationChanged if needed
  → MainWindow initial evaluation → Show
```

MainWindow subscribes before Router initialization, so it cannot miss the initial state notification. The existing Mutex, `--startup` argument, normal-launch settings window, and update check remain in place.

## HUD Switch Lifecycle

```text
NavigateToAsync(newHudId)
  → validate target and resolve fallback
  → unsubscribe old module → old DeactivateAsync
  → new InitializeAsync (first time only) → subscribe new module
  → new ActivateAsync (defer invalidations)
  → commit CurrentHud → StateChanged
  → one PresentationChanged if deferred
  → MainWindow reevaluates
```

The new subscription is installed before Activate; the old subscription is removed before Deactivate. On failure, Router cleans up the new module and tries the old module, then the effective default. If recovery fails, it sets the current HUD to `null`, presentation to `Collapsed`, initialization to false, and throws `HudNavigationException`. When `DisableAsync` disables the current HUD, Router resolves a fallback from the updated list before switching.

`HudNavigationViewModel` exposes the registered modules to both navigation chrome and settings. It persists enablement and ordering immediately, prevents disabling the final HUD, cycles through enabled modules, and keeps the configured startup HUD valid. A module switch preserves the current expanded or pinned presentation; MainWindow animates the content and resized shell.

## Shutdown Lifecycle

```text
shutdown request from window / menu / tray
  → MainWindow exit animation
  → App.RequestShutdownAsync (join concurrent requests to one Task)
  → MainWindow.DetachHudRouter
  → HudNavigationViewModel.Dispose
  → HudRouter.ShutdownAsync
       → unsubscribe current module → DeactivateAsync → clear CurrentHud
  → HudRegistry.DisposeAsync
       → DisposeAsync every module exactly once
       → MusicHudModule runs Cleanup once
  → release shell resources → AllowClose → Close → Application.Shutdown
```

`App.OnExit` disposes only the Mutex and does not start new asynchronous shutdown work. `MainWindow.OnClosed` performs only idempotent final cleanup. MainWindow, Router, and App.OnExit never call Cleanup directly.

## Settings SchemaVersion and Migration

`UserSettings` includes `SchemaVersion` (currently 1), `DefaultHudId` (default `music`), `EnabledHudModuleIds` (default `["music", "home"]`), `HudNavigationPlacement` (default `RightRail`), `Modules`, and `AdditionalProperties` for unknown top-level JSON while retaining existing settings. Existing settings without the Home introduction marker receive Home once after Music; subsequent manual disablement is preserved.

`UserSettingsJson` performs pure structural normalization. Only missing, zero, or negative schema versions are upgraded; future versions greater than the current value are preserved. Unregistered IDs, unknown module JSON, and unknown top-level JSON are round-tripped. Normalization and serialization create a new settings object, collections, and cloned `JsonElement` values without mutating the caller. Malformed JSON falls back to defaults. `SettingsService` retains `%AppData%\NoraBar\settings.json` and migration from the legacy location.

The settings layer preserves unknown IDs and an empty enabled list. Only Router corrects runtime executability, preventing data loss for future-version or temporarily unavailable HUDs.

## Adding a Built-in HUD

Add a stable ID to `BuiltInHudIds`, implement `IHudModule`, and register it in `App.OnStartup`. Then enable it through settings, optionally make it the default, and test lifecycle, fallback, view, size, and disposal.

```csharp
public sealed class ClockHudModule : IHudModule
{
    private readonly FrameworkElement _cachedView = new ClockHudView();

    public string Id => BuiltInHudIds.Clock;
    public HudModuleMetadata Metadata { get; } = new("Clock", 10);
    public event EventHandler? PresentationInvalidated;
    public ValueTask InitializeAsync(CancellationToken token) => ValueTask.CompletedTask;
    public ValueTask ActivateAsync(CancellationToken token) => ValueTask.CompletedTask;
    public ValueTask DeactivateAsync(CancellationToken token) => ValueTask.CompletedTask;
    public FrameworkElement GetView(HudViewContext context) => _cachedView;
    public HudSize GetPreferredSize(HudViewContext context) => new(320, 96);
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

registry.Register(new ClockHudModule());
```

```json
{
  "DefaultHudId": "clock",
  "EnabledHudModuleIds": ["music", "clock"],
  "Modules": { "clock": {} }
}
```

`BuiltInHudIds.Clock`, `ClockHudModule`, and its view are illustrative only and are not implemented in Phase 0.

## Launcher HUD ownership

`BuiltInHudIds.Launcher` is the third built-in module. `App.OnStartup` owns construction of `LauncherSettingsViewModel`, `LauncherHudViewModel`, `LauncherHudModule`, the Windows platform boundary, application catalog, WinEvent tracker, usage store, icon cache, and global-hotkey registrar. The registry disposes the module; the module disposes its presentation source; the source stops and disposes hooks, catalog cancellation, timers, and local usage persistence. Settings preview uses a separate inert view model and never owns or invokes the live runtime.

Launcher configuration is stored as a versioned payload in `UserSettings.Modules["launcher"]`. The writer overlays known fields onto the existing JSON object so unknown module data survives. A separate introduction marker adds Launcher once for v1.1.0 users without overriding a later manual disable. Runtime fallback and Rules never rewrite pinned configuration.

Windows-specific code stays behind `ILauncherPlatform`, `ILauncherWindowTracker`, and `ILauncherApplicationCatalog`. Shell discovery runs lazily on an STA worker. WinEvent hooks drive debounced running-window refreshes; callback delegates remain rooted and disposal waits for in-flight callbacks after unhooking. The bounded icon cache returns frozen images, while local usage is throttled to `%LocalAppData%\NoraBar\Launcher\usage.json`. Global shortcuts are registered against the main HWND and are removed before the HWND is destroyed.

`MainWindow` remains module-agnostic. Its only Launcher-adjacent APIs accept an ordinal HUD ID for opening Settings or navigating with the existing fullscreen expansion policy. Pages, groups, search, Smart scoring, Rules, execution, and window commands remain under `Hud/Launcher` and `Views/Launcher`.
## Phase 0 External Plug-in Boundary

```text
external plug-in loading (future)
  → IHudModule → HudRegistry → HudRouter → MainWindow
```

The implemented boundary stops at `IHudModule`, Registry, Router, and the generic host. An external DLL loader, assembly scanning, directory watching, isolated loading, signing and trust management, and a plug-in store are not implemented. Phase 0 does not provide a separate SDK or external binary compatibility guarantee. External plug-in context switching remains out of scope; Launcher Rules are an in-process feature of the built-in module.

## Existing ViewModels and Services

`MainViewModel` holds settings and shell state and remains the music view compatibility DataContext. `HudNavigationViewModel` coordinates enabled order, startup selection, settings selection, and navigation commands. `HomeHudViewModel` adapts music state and clock data for Home views. `LauncherSettingsViewModel` owns user configuration editing and `LauncherHudViewModel` owns Launcher runtime presentation. `MusicViewModel` provides track, playback, lyrics, waveform, and controls. `MediaControlService` handles Windows media sessions, `AudioVisualizerService` performs audio analysis, `LyricsService` supplies lyrics, `SettingsService` persists settings, `StartupService` handles auto-start, and `LocalizationService` manages language strings.

## Japanese Version

- [[アーキテクチャ|アーキテクチャ]]
