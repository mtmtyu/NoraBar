# Getting Started

NoraBar stays near the center of the top edge of the screen after launch. It is almost invisible while idle, then opens as a music HUD when you move the mouse near it.

## Requirements

- Windows 10 or later
- [.NET Desktop Runtime 10](https://dotnet.microsoft.com/download/dotnet/10.0)

NoraBar targets `net10.0-windows`, so it requires the .NET 10 Desktop Runtime to run. Install the latest .NET Desktop Runtime 10.x for your Windows architecture. The SDK, Visual Studio, and the .NET CLI are not required for normal use.

## Run NoraBar

Download a release package from GitHub Releases and start NoraBar.

### Portable Version

1. Download the portable zip file.
2. Extract it to any folder.
3. Start the NoraBar executable in the extracted folder.

### Installer Version

1. Download the installer.
2. Run the installer.
3. Start NoraBar after installation.

For source build and development instructions, see [[Development]].

## First Steps

1. Play media in software.
2. Move the mouse near the center of the top edge of the screen.
3. The HUD opens and shows the track title, artist, album art, playback controls, waveform, progress, and synced lyrics when available.
4. Move the mouse away, and the HUD returns to its thin idle state.

## Music Controls

The HUD can control these actions:

- Previous track
- Play / pause
- Next track

NoraBar uses Windows global media sessions, so it works with music apps and browser playback that support Windows media controls.

## Open Settings

You can open the settings window from either place:

- Double-click the NoraBar icon in the system tray
- Right-click the HUD and select Settings

During a normal launch, the settings window opens automatically. During startup launch, NoraBar receives the `--startup` argument and stays resident without opening the settings window.

## Quit NoraBar

Select Quit from the system tray menu or the HUD right-click menu. When exiting, the HUD fades upward.

## Japanese Version

- [[はじめ方|はじめ方]]
