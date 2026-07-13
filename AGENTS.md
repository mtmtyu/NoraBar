# NoraBar Agent Guide

## Project overview

NoraBar is a Windows desktop HUD built with WPF on .NET 10. It presents media controls, lyrics, progress, session selection, and shell behavior in a compact top-edge window. The application project is `NoraBar/NoraBar.csproj`; the xUnit test project is `NoraBar.Tests/NoraBar.Tests.csproj`.

Treat the current source, project files, tests, and architecture documents as authoritative. Plans under `docs/superpowers/plans/` provide historical context only and may describe work that has already changed.

## Repository map

- `NoraBar/App.xaml.cs`: composition root, single-instance startup, application shutdown orchestration.
- `NoraBar/MainWindow.xaml` and `NoraBar/MainWindow.xaml.cs`: generic HUD shell and Windows-specific window behavior.
- `NoraBar/Hud/`: module contract, registry, router, snapshots, presentation states, and shared HUD value types.
- `NoraBar/Hud/Music/`: built-in music module, its layout rules, and design-specific views.
- `NoraBar/ViewModels/`: application and presentation view models. `MainViewModel` remains the compatibility context for the music HUD and shell settings.
- `NoraBar/Services/`: settings persistence, media services, shutdown coordination, and other application services.
- `NoraBar/Views/`: WPF views and settings UI.
- `NoraBar.Tests/`: xUnit tests grouped by architecture, HUD, services, settings, and view models.
- `docs/wiki/Architecture.md` and `docs/wiki/アーキテクチャ.md`: maintained English and Japanese architecture references.

Search for an existing type or pattern before adding one. Prefer the current module, router, shutdown, and settings abstractions over parallel implementations.

## Build and test commands

Run commands from the repository root:

```powershell
dotnet restore NoraBar.slnx
dotnet build NoraBar.slnx -c Release
dotnet test NoraBar.slnx -c Release
dotnet build NoraBar.slnx -c Debug
```

Use a focused test while iterating, then run the complete Release suite before completion:

```powershell
dotnet test NoraBar.Tests/NoraBar.Tests.csproj -c Release --filter FullyQualifiedName~HudRouterTests
```

This repository has no Node package manifest. Do not substitute npm, ESLint, or React tooling for the .NET commands above.

NoraBar is Windows-specific. If the environment cannot display or interact with WPF UI, report manual GUI verification as `NOT RUN`; never claim it passed. Automated build and test results remain required.

## Core HUD architecture

### Composition root

`App.OnStartup` is the composition root. `App.xaml` deliberately has no `StartupUri`. Construct `MainViewModel`, built-in modules, `HudRegistry`, `HudRouter`, and `MainWindow` explicitly and connect them through constructor injection. Preserve the mutex, startup arguments, settings-window behavior, and explicit shutdown flow.

Do not introduce a service locator or hide startup ownership in views. A new built-in module is registered here before router initialization.

### IDs and presentation state

HUD IDs remain ordinal strings. Built-in IDs belong in `BuiltInHudIds`; never scatter string literals such as `"music"`. Registries and settings-related ID collections use `StringComparer.Ordinal` or `StringComparison.Ordinal`.

`HudPresentationState` describes shell presentation only: `Collapsed`, `Peek`, `Expanded`, and `Pinned`. A presentation-state change is not a module lifecycle transition.

### `IHudModule`

`IHudModule` is the stable in-process boundary for a HUD implementation. A module owns:

- identity and metadata;
- idempotent initialization, activation, deactivation, and asynchronous disposal;
- view selection through `GetView(HudViewContext)`;
- preferred size through `GetPreferredSize(HudViewContext)`;
- `PresentationInvalidated` when module-owned inputs can change its current view or preferred size.

Avoid duplicate event subscriptions, service starts, service stops, or cleanup on repeated lifecycle calls. `GetView` and `GetPreferredSize` are UI-facing synchronous methods; do not perform blocking I/O in them.

### `HudRegistry`

`HudRegistry` owns registered module instances. It rejects invalid or duplicate IDs, preserves registration order for deterministic fallback, resolves IDs with ordinal comparison, and disposes every registered module once. Registration is a startup concern; do not mutate registration during active routing unless the architecture is deliberately extended and covered by tests.

### `HudRouter`

`HudRouter` is the sole owner of current-HUD selection, effective enabled/default configuration, presentation state, module lifecycle transitions, and module `PresentationInvalidated` subscriptions. It serializes initialization, navigation, disabling, and shutdown. Presentation-state APIs must also coordinate with transitions and shutdown.

Consumers read one immutable `HudRouterSnapshot`, not separate router properties. The snapshot makes the current ID, current module, presentation state, initialization state, and shutdown state coherent. Publish events outside locks. Do not publish a target module before activation succeeds, publish intermediate transition state, or emit redundant notifications for an unchanged presentation state.

The router validates settings at runtime without rewriting them. Invalid, disabled, duplicate, or unregistered IDs are filtered from the effective configuration, and the built-in music HUD is the safe fallback when necessary.

## Lifecycle invariants

### Startup

The startup ownership order is:

1. Create `MainViewModel` and built-in modules.
2. Create `HudRegistry` and register the modules.
3. Create `HudRouter`.
4. Create `MainWindow`, which subscribes to router notifications.
5. Initialize the router. It resolves the effective default, initializes that module once, subscribes to its invalidation before activation, activates it, and then publishes the current HUD.
6. Evaluate the initial snapshot, view, and preferred size, then show the window.

### Presentation changes

Pointer enter and leave change only router presentation state. Enter expands; leave collapses unless pinned or shell policy suppresses the change. Transitions among `Collapsed`, `Peek`, `Expanded`, and `Pinned` must not call `ActivateAsync` or `DeactivateAsync`.

### Navigation and disabling

Resolve and validate the destination, including fallback, before disturbing the current HUD. For a module switch, preserve this order:

1. Unsubscribe the old module before deactivation.
2. Deactivate the old module.
3. Initialize the new module once.
4. Subscribe to the new module before activation so activation-time invalidations are captured.
5. Activate the new module.
6. Commit the new current HUD and publish router notifications.

If a transition fails, preserve or recover a coherent current module and aggregate cleanup failures rather than hiding them. Disabling the current HUD must resolve an enabled fallback first. Never permit an effective configuration with no HUD; the music module is the Phase 0 safety fallback.

### Shutdown

All exit paths—menu, tray, Alt+F4, system menu, and OS close—must converge on the same guarded asynchronous shutdown request. `MainWindow.OnClosing` cancels ordinary close attempts until the application has called `AllowClose()`. Do not start duplicate exit animations or shutdown tasks.

Preserve this ownership order:

1. `MainWindow` detaches router event handlers.
2. `HudRouter.ShutdownAsync` unsubscribes and deactivates the current module.
3. `HudRegistry.DisposeAsync` disposes registered modules once.
4. The application releases the window, tray, and other shell resources, allows the final close, and shuts down WPF.

`MusicHudModule.DisposeAsync` is the only owner of `MusicViewModel.Cleanup`. Never call that cleanup directly from `MainWindow`, `HudRouter`, or `App`.

## MainWindow boundary

`MainWindow` is a generic shell. For HUD view selection and preferred size, it observes only router `StateChanged` and `PresentationChanged`. On either notification, obtain exactly one `HudRouterSnapshot`, then evaluate `GetView` and `GetPreferredSize` from that snapshot. Do not subscribe directly to a module's `PresentationInvalidated`; the router owns and normalizes that signal.

Keep shell concerns in the shell layer: localization, window position, multi-monitor and DPI behavior, position editing, pointer handling, animation, fullscreen expansion suppression, tray UI, settings-window coordination, and exit state. Keep module-specific views, size rules, lyrics/progress/session conditions, and service lifetime out of `MainWindow`.

`MainWindowDependencyTests` enforces this boundary. Do not bypass it with aliases, reflection, string-based type lookup, or a new shell dependency on music-specific types.

## Music HUD rules

`MusicHudModule` adapts the existing `MainViewModel` and music services to `IHudModule`. It owns the Minimal, Productivity, and Lyrics Focus designs. Cache one WPF view per design and reuse it; do not recreate views on every refresh. Respect WPF dispatcher affinity when creating or retrieving cached views.

Settings changes should select the appropriate cached view, recompute preferred size, update `DataContext` only when needed, and raise `PresentationInvalidated` only when presentation may have changed. `MusicHudLayout` owns music-specific sizing rules, including progress, lyrics, and multiple-session inputs.

Initialization attaches source subscriptions once. Activation and deactivation preserve the existing music-service start/stop timing. Disposal removes subscriptions, waits for in-flight invalidation work where required, and invokes cleanup once even when disposal is retried.

## Adding a built-in HUD

Use this checklist:

1. Add an ordinal ID constant to `BuiltInHudIds`.
2. Implement `IHudModule` with explicit, idempotent lifecycle ownership.
3. Keep its views, view cache, sizing logic, settings, and services inside its module area.
4. Add a module settings payload under `UserSettings.Modules` without removing unknown payloads.
5. Construct and register the module in `App.OnStartup` before router initialization.
6. Add registry, router, lifecycle, fallback, view-cache, sizing, and settings round-trip tests as applicable.
7. Verify that `MainWindow` remains module-agnostic and update both architecture documents when responsibilities change.

Do not add an external plugin loader merely to add a built-in HUD.

## Settings compatibility

`SettingsService.UserSettings` and `UserSettingsJson` define the persistence boundary. Preserve `SchemaVersion`, `DefaultHudId`, `EnabledHudModuleIds`, module payloads, unknown top-level JSON, unknown module JSON, and unregistered HUD IDs during round trips whenever possible.

Normalization and serialization must not unexpectedly mutate the caller's object. Only missing, zero, or negative legacy schema values are promoted to the current schema. A schema newer than the application understands must retain its version and unknown JSON; never silently downgrade it on save.

Runtime fallback is not a migration. The router may compute safe effective IDs, but it must not overwrite persisted user choices merely because this build cannot resolve them. Changes to settings shape require backward-compatibility and future-version tests.

## Testing expectations

Follow RED/GREEN/refactor for behavioral changes. Add the narrowest failing test first, implement the behavior, then simplify without changing semantics. Reuse `FakeHudModule` for router and registry lifecycle scenarios.

At minimum, test the layers affected by a change:

- registry ownership and ordinal ID behavior: `HudRegistryTests`;
- router ordering, snapshots, concurrency, fallback, invalidation, and shutdown: `HudRouterTests`;
- music view caching, dispatcher use, invalidation, and cleanup: `MusicHudModuleTests` and `MusicHudLayoutTests`;
- settings preservation and nonmutation: `UserSettingsJsonTests` and `MainViewModelSettingsTests`;
- shell dependency boundaries: `MainWindowDependencyTests`;
- joined shutdown requests and best-effort cleanup: service tests.

Run focused tests during development, then restore, Release build, full Release test, and Debug build before handing off. Do not edit tests only to conceal a product defect.

## Coding rules

- Prefer simple, explicit, type-safe C# with nullable reference types enabled.
- Match existing naming, formatting, async, locking, and exception-aggregation patterns.
- Avoid magic values; centralize stable IDs and layout constants.
- Avoid unnecessary comments. Comments should explain why a constraint exists, not restate code.
- Keep event subscription ownership visible and symmetric.
- Do not block on asynchronous lifecycle operations with `.Wait()`, `.Result`, or synchronous dispatcher waits.
- Raise events and call external code outside locks. Keep lock scopes short.
- Preserve user changes in a dirty worktree and avoid unrelated formatting or refactors.
- Do not add a production dependency when the platform or an existing library already solves the problem. If a dependency is necessary, document its purpose and license impact.

## Documentation rules

Update `docs/wiki/Architecture.md` and `docs/wiki/アーキテクチャ.md` together when architecture, lifecycle ordering, ownership, extension boundaries, or settings guarantees change. Keep both versions semantically aligned. Update `README.md` when user-visible capabilities or setup instructions change.

Document durable current behavior, not transient branch names, commit hashes, test totals, or implementation diary entries.

## Git workflow

- Work on a dedicated `codex/` branch unless the user specifies another non-default branch.
- Keep commits small and aligned with one verified work unit.
- Use Conventional Commits. Repository history may include a Japanese subject or a bilingual subject; follow the surrounding history and the user's requested language.
- Before committing, inspect `git status`, `git diff --check`, and the exact staged diff. Stage only intended files.
- Never rewrite or discard unrelated user changes.
- Do not push, create a pull request, or merge unless the user explicitly requests it.

## Non-goals and extension boundary

The modular HUD foundation is an in-process built-in module system. It does not currently provide external DLL discovery, assembly scanning, directory watching, isolated `AssemblyLoadContext` loading, package distribution, signing or trust policy, permissions, crash isolation, or a public binary-compatible SDK.

If external modules are introduced later, keep the core flow intact: a loader produces `IHudModule` instances, the registry owns them, the router controls their lifecycle and presentation, and `MainWindow` remains a generic shell. Define trust, compatibility, isolation, failure handling, and unload behavior before accepting third-party code.
