# NoraBar Wiki

Welcome to the Wiki for NoraBar, a small music HUD that quietly lives at the top edge of your screen.

NoraBar is a Windows desktop app that keeps music close without cluttering your workspace. It usually waits as a 2px strip, then opens into a HUD with track information, album art, playback controls, and a waveform visualizer when you move the mouse near it.

## Language

- [[日本語版|ホーム]]

## Start Here

- [[Guide-Getting-Started]] - Launching NoraBar, basic controls, and first-use flow
- [[Guide-Configuration]] - Design style, progress bar, startup, language, and update checks
- [[Troubleshooting]] - Things to check when music information or the waveform does not appear

## For Developers

- [[Development]] - Development environment, build commands, verification, and Wiki sync
- [[Architecture]] - How the window, ViewModels, and services fit together

## Highlights

- A subtle top-edge HUD that opens only on hover
- Music controls backed by Windows media sessions
- An 8-bar waveform visualizer powered by WASAPI loopback capture
- Two design styles: Minimal and Productivity
- Japanese / English display language switching
- System tray residency and Windows startup registration

## Editing This Wiki

The source for this Wiki lives in `docs/wiki/` in the repository.

When changes are pushed to the `main` branch, `.github/workflows/wiki-sync.yml` syncs these Markdown files to the GitHub Wiki. Use GitHub Wiki page links such as `[[Guide-Getting-Started]]` for links between Wiki pages.
