# Development Guide

This page is for people who want to build or modify NoraBar.

## Project Structure

```text
NoraBar.slnx
NoraBar/
  App.xaml
  MainWindow.xaml
  Models/
  Services/
  ViewModels/
  Views/
docs/wiki/
.github/workflows/wiki-sync.yml
```

The main application code lives under `NoraBar/`. Wiki source files live under `docs/wiki/`.

## Build

```powershell
dotnet restore NoraBar\NoraBar.csproj
dotnet build NoraBar\NoraBar.csproj
```

Use this command to run the app:

```powershell
dotnet run --project NoraBar\NoraBar.csproj
```

To build a portable version:
```powershell
dotnet publish NoraBar/NoraBar.csproj -c Release -r win-x64 --self-contained false -o out/portable
```

## Main Dependencies

- WPF
- Windows Forms `NotifyIcon`
- Windows Media Control API
- CSCore `1.2.1.2`
- Material.Icons.WPF `3.0.2`

Music information comes from Windows media sessions. The waveform captures system audio through WASAPI loopback and visualizes it after FFT processing.

## Where to Look When Changing Code

- HUD display state is managed by `IslandState`.
- HUD appearance is switched by `DesignVariant`.
- Settings are read and written through `SettingsService`.
- Display language strings are managed by dictionaries in `LocalizationService`.
- Music controls are handled by `MediaControlService`, and waveform display is handled by `AudioVisualizerService`.

## Wiki Sync

Markdown files under `docs/wiki/` are synced to the GitHub Wiki when changes are pushed to the `master` branch. The sync workflow is defined in `.github/workflows/wiki-sync.yml`.

Use GitHub Wiki page links between Wiki pages:

```markdown
[[Guide-Getting-Started]]
```

## Release Versioning

Before publishing a new release, update the version fields in `NoraBar/NoraBar.csproj`:

- `Version`
- `AssemblyVersion`
- `FileVersion`

Also update `AppVersion` in `setup.iss` and `CurrentVersion` in `MainViewModel.cs`.

These values should match the release version so the in-app update checker can correctly compare the installed version with the latest GitHub Release.

## Documentation Guidelines

- Document only implemented features.
- Match screen labels and setting names to the app UI.
- Keep procedures short and explain only the places that may cause confusion.
- When documenting old names or compatibility paths, also explain why they exist.

## Japanese Version

- [[開発ガイド|開発ガイド]]
