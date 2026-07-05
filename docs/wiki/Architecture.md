# Architecture

NoraBar uses a WPF structure that is close to MVVM. Screens are written in XAML, state lives in ViewModels, and Windows integrations are separated into services.

## Overview

```text
MainWindow
  -> MainViewModel
       -> MusicViewModel
            -> MediaControlService
            -> AudioVisualizerService
       -> SettingsService
       -> StartupService
       -> LocalizationService
  -> DesignAMusicView / DesignBMusicView
```

## MainWindow

`MainWindow` is a transparent window that stays at the top edge of the screen.

- `Topmost=True` keeps it above other windows.
- `ShowInTaskbar=False` keeps it out of the taskbar.
- While idle, it is a thin `200x2` area.
- On mouse hover, it enters the `Music` state and shows the selected design view.
- When the mouse leaves, it returns to `Idle` and closes the content.

## MainViewModel

Holds app-wide settings and screen state.

- Current design style
- Current HUD state
- Progress bar visibility
- Display language
- Settings window page
- Update check dialog
- License dialog

When a setting changes, it calls `SettingsService.Save` to persist the change.

## MusicViewModel

Collects the state required for the music display.

- Track title
- Artist
- Album art
- Playback state
- Playback position
- Total duration
- Progress ratio
- Waveform data

It also exposes commands for play, pause, previous track, and next track.

## Services

### MediaControlService

Uses Windows `GlobalSystemMediaTransportControlsSessionManager` to read track information and playback state from the current media session.

Playback position is updated by a timer. When media is playing, the service adjusts the position using elapsed time from `LastUpdatedTime`.

### AudioVisualizerService

Captures system audio with CSCore `WasapiLoopbackCapture`, then converts FFT results into 8 visual bars.

It uses lower-frequency bands, converts them to decibels, and normalizes the values to a `0.0` to `1.0` range.

### SettingsService

Saves user settings as JSON.

```text
%AppData%\NoraBar\settings.json
```

The settings directory name is `NoraBar`, matching the app name.

### StartupService

Creates or removes `NoraBar.lnk` in the Windows Startup folder. The shortcut includes the `--startup` argument so the settings window does not open automatically during startup launch.

### LocalizationService

Manages Japanese and English UI strings in dictionaries. When the setting changes, the ViewModel raises change notifications for related properties so the UI updates.

## Views

The HUD has two views.

- `DesignAMusicView`: compact Minimal display
- `DesignBMusicView`: wider Productivity display with more information

Both read `MusicViewModel` state through `MainViewModel.Music`.

## Japanese Version

- [[アーキテクチャ|アーキテクチャ]]
