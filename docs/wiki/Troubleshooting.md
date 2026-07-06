# Troubleshooting

Start here when NoraBar does not appear or behave as expected.

## The HUD Does Not Appear

- Move the mouse near the center of the top edge of the screen.
- NoraBar normally waits as a thin `200x2` area. It is expected to be almost invisible while idle.
- Check whether the NoraBar icon is visible in the system tray.

## Track Title Does Not Appear

- Play media in your music app or software.

## Playback Buttons Do Not Work

- The target music app must support Windows global media controls.
- If multiple music apps are open, Windows controls the app it currently recognizes as the active media session.

## The Waveform Does Not Move

- Confirm that audio is playing from the PC.
- Confirm that the output device is enabled.
- If WASAPI loopback initialization fails, the waveform is not displayed. Restarting the app may recover it.

## Lyrics Do Not Appear

- Make sure Synced Lyrics is enabled in Settings.
- Lyrics are provided by LRCLIB, so not every track has synced lyrics.
- Check your network connection.
- Track title, artist, album name, and duration are used to search lyrics, so incorrect media metadata may prevent matching.

## Settings Are Not Saved

Settings are saved here:

```text
%AppData%\NoraBar\settings.json
```

Confirm that this folder is writable.

## Startup Launch Does Not Work

When Run at system startup is enabled in the settings window, NoraBar creates `NoraBar.lnk` in the Windows Startup folder.

Confirm that the shortcut has not been removed from the Startup folder.

## Update Check Fails

Update checks access GitHub Releases. Check the network connection, proxy settings, and whether GitHub access is restricted.

## Japanese Version

- [[トラブルシューティング|トラブルシューティング]]
