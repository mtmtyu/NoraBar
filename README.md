# NoraBar

<img src="NoraBar/Assets/AppIcon.png" width="128" height="128" alt="NoraBar Icon">

[日本語版 README](README.ja.md)

I am very happy to be able to release this software!

In this initial release, I have focused primarily on music control features. While the functionality is still limited, I plan to add a variety of features in the future to make this software even more useful and enjoyable.

A small music HUD that quietly appears at the very top of your screen.

NoraBar is a WPF application for Windows desktop. It usually waits as a thin 2px strip at the top edge of the screen, then opens into a music controller when you move the mouse near it. It keeps the current track title, artist, album art, playback position, and waveform visualizer in a place that stays close without getting in your way.

## Features

- **Top-edge HUD**
  - Stays subtle while idle and expands only on hover.
  - Runs as a topmost window so it is ready when you need it.

- **Music app integration**
  - Reads the track title, artist, album art, playback state, and playback position from Windows media sessions.
  - Controls play, pause, previous track, and next track directly from the HUD.

- **Audio-reactive waveform visualizer**
  - Uses CSCore WASAPI loopback capture to visualize the audio currently playing on the PC with 8 bars.
  - Adds a small sense of live motion without taking over the desktop.

- **Synchronized lyrics display**
  - Fetches and displays synchronized lyrics for the current track using LRCLIB.

- **Two design styles**
  - `Minimal Floating Pill`: a compact, lightweight style for everyday use.
  - `Productivity Command Island`: a wider style that gives album art and progress more room.

- **Settings window**
  - Switch the design style, progress bar visibility, startup behavior, and display language.
  - Customize the HUD position by dragging it in edit mode.
  - Includes GitHub Releases update checks and third-party license information.

## Screenshots

| Minimal Floating Pill | Productivity Command Island |
| --- | --- |
| ![Minimal Floating Pill](docs/images/screenshot-1.png) | ![Productivity Command Island](docs/images/screenshot-2.png) |


## Requirements

- Windows 10 or later
- [.NET Desktop Runtime 10](https://dotnet.microsoft.com/download/dotnet/10.0)

NoraBar targets `net10.0-windows`, so it requires the .NET 10 Desktop Runtime to run. Install the latest .NET Desktop Runtime 10.x for your Windows architecture. The SDK, Visual Studio, and the .NET CLI are only needed when building from source.

## Run

Download the portable version or installer from GitHub Releases, then start NoraBar.

- Portable version: extract the zip file to any folder and run the NoraBar executable.
- Installer version: run the installer, then start NoraBar after installation.

During a normal launch, the settings window also opens. When NoraBar is launched from Windows startup, it receives the `--startup` argument and quietly keeps only the HUD resident.

For source build and development instructions, see the [Development Guide](docs/wiki/Development.md).

## Usage

1. Start NoraBar.
2. Move the mouse near the center of the top edge of the screen.
3. When the HUD opens, check the track information and control buttons.
4. Open settings from the right-click menu or the system tray.

Settings are saved to `%AppData%\NoraBar\settings.json`.

## Wiki

More detailed usage and design notes are available in the Wiki.

- [Wiki Home](docs/wiki/Home.md)
- [Getting Started](docs/wiki/Guide-Getting-Started.md)
- [Configuration Guide](docs/wiki/Guide-Configuration.md)
- [Development Guide](docs/wiki/Development.md)
- [Architecture](docs/wiki/Architecture.md)
- [Troubleshooting](docs/wiki/Troubleshooting.md)

Japanese Wiki pages are also available:

- [Wiki Home 日本語版](docs/wiki/ホーム.md)
- [はじめ方](docs/wiki/はじめ方.md)
- [設定ガイド](docs/wiki/設定ガイド.md)
- [開発ガイド](docs/wiki/開発ガイド.md)
- [アーキテクチャ](docs/wiki/アーキテクチャ.md)
- [トラブルシューティング](docs/wiki/トラブルシューティング.md)

Markdown files under `docs/wiki/` are synced to the GitHub Wiki when changes are pushed to the `main` branch.

## Tech Stack

- C# / WPF
- .NET
- XAML
- Windows Media Control API
- CSCore
- Windows Forms NotifyIcon

## License

NoraBar uses CSCore as a third-party library. License information is available from the settings window in the app.

NoraBar is licensed under the GNU Affero General Public License v3.0. See [LICENSE](LICENSE) for details.

## About the Name

NoraBar is designed as a small notch-like place at the top of the screen that offers only the information you need.

The star is not the app; it is your work. NoraBar aims to be a modest, thoughtful HUD that rests above it.
