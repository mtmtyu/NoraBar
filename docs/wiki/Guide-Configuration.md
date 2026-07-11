# Configuration Guide

The NoraBar settings window lets you adjust the HUD appearance and how the app stays resident. Settings are saved as soon as they change.

## General Settings

### Run at System Startup

Creates `NoraBar.lnk` in the Windows Startup folder so NoraBar launches when you sign in.
On the first run (when the settings file does not exist yet), this setting is enabled by default.

Startup launches pass the `--startup` argument, so only the HUD stays resident and the settings window does not open automatically.

### Check for Updates on Startup

When enabled, NoraBar automatically checks for a new version in the background when the application starts. If an update is detected, the settings window will open automatically, showing the update details and a button to download the latest release.

### Display Language

Switches the language used in the app.
On the first run, "Japanese" is selected if the system language (OS language) is Japanese, and "English" is selected for other languages.

- Japanese
- English

This affects the settings window, tray menu, and right-click menu.

### Reset Settings

Resets all settings to their default values.


## HUD Settings

### Shared Display Settings

#### Window Position

Lets you customize the horizontal position of the HUD.

Use Change Position to enter position edit mode, drag the HUD to the desired position, then finish editing. Use Reset to Default to return the HUD to the default centered position.

#### Disable Expand On Fullscreen

Disables HUD expansion while a fullscreen application is running on the HUD's screen. This prevents the HUD from opening accidentally during games or fullscreen videos.


### Selected HUD Settings

#### HUD Selection

Select the desktop HUD to configure. Currently, the "Music HUD" is available.

#### Design Style

You can switch between `Minimal Floating Pill` and `Productivity Command Island`.

- `Minimal Floating Pill`
  - A lightweight view around 450px wide.
  - Keeps the track title, artist, album art, waveform, and control buttons compact.

- `Productivity Command Island`
  - A wider view around 560px wide.
  - Gives album art more space and makes playback time easier to read.

#### Show Progress Bar

Toggles the bar that shows the playback position.

When enabled, Minimal shows a thin progress bar, and Productivity shows a progress bar with the current position and total duration.

#### Synced Lyrics

Toggles synced lyrics in the HUD.

When enabled, NoraBar fetches synced lyrics from LRCLIB when available and displays the current lyric line in sync with playback. If lyrics are not found or a network error occurs, a short status message is shown.

#### Text Scroll Mode

Controls how track titles and artist names scroll when they are too long to fit in the HUD.

- `Disabled` (Default): Text does not scroll automatically.
- `Scroll on Hover`: Scrolls the text only when you move the mouse pointer over the track information.
- `Always Scroll`: Scrolls the text continuously.

#### Restart Audio Visualizer

Resets the audio visualizer. If the waveform visualizer stops reacting or fails to initialize, you can restart only the visualizer component with this button without needing to restart the entire application.


## About

### Check for Updates

Checks the latest release on GitHub Releases. When manual check is performed and a newer version is found, a button appears to open the release page. If startup checks are enabled, the settings window will open automatically on startup with the update notification if a new version is available.

### GitHub Repository

Opens the repository page that contains the NoraBar source code.

### Open Source Licenses

Shows the licenses for third-party libraries used by NoraBar. The app currently includes CSCore, Material.Icons.WPF, and LRCLIB license information.

## Settings File

Settings are saved here:

```text
%AppData%\NoraBar\settings.json
```

The settings directory name is `NoraBar`, matching the app name.

## Japanese Version

- [[設定ガイド|設定ガイド]]
