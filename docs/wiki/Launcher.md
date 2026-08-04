# Launcher HUD

Launcher is NoraBar's compact application and resource launcher. It complements a busy Windows taskbar while keeping editing in Settings and execution in the HUD.

## Pages, groups, and items

Open **Settings → HUD Settings → Launcher HUD → Pages & Items**. A page contains ordered groups, and each group contains ordered items. IDs are stable and independent from display names, so renaming or relocating an item does not change its identity.

Items can launch Win32 applications, packaged/Microsoft Store applications, files, folders, and HTTP/HTTPS URLs. Add items from the installed-app catalog, currently running applications, file or folder pickers, the URL editor, or by dropping Explorer files onto Launcher Settings. A dropped item is shown in the editor before it is saved. Custom images and icons can be selected per item.

The normal HUD is intentionally not an editor. Its context menu can open, switch windows, request a graceful close, confirm a force quit, open another instance, run applicable Win32 programs as administrator, show the file location, or open the corresponding item in Settings.

Missing files are retained as recoverable items. Use **Locate…** in Settings to update the target without changing the item ID, or remove it explicitly.

## Presentation and search

Peek shows up to seven high-priority items from the current page, followed by Smart suggestions when enabled. Expanded and Pinned show the page selector, search field, groups, and item grid. Typing while expanded enters search; Up/Down selects a result, Enter launches it, and Escape clears search. Search covers registered Launcher items and the asynchronously loaded installed-app catalog, not arbitrary files or the web.

Clicking a stopped application launches it. Clicking a running application restores and focuses its most recently active top-level window. Shift-click requests a new instance. Applications can still enforce their own single-instance policy.

## Smart suggestions and privacy

Smart ranks launchable applications using an explainable local score based on recency with decay, launch frequency, foreground duration, time-of-day affinity, and foreground-application context. Visible items and missing targets are excluded, and histories are bounded.

Usage data is stored only in `%LocalAppData%\NoraBar\Launcher\usage.json`. NoraBar does not transmit Launcher usage or add telemetry. Writes are throttled, and **Smart → Clear Launcher usage history** deletes the local history.

## Rules

Rules select an effective page from optional weekday, time-range, and foreground-application conditions. Conditions in one rule use AND semantics. Higher priority wins; configured order breaks ties. Rules never edit pages, groups, or pinned items. A manually selected page remains in effect for the current expanded interaction, and automatic evaluation resumes after collapse or the next open.

## Global shortcuts

The optional **Open Launcher** and **Open Launcher Search** shortcuts are unassigned by default. Chords require a modifier and may fail when another program owns the same chord. NoraBar keeps the previous working registration if replacement fails and unregisters shortcuts at shutdown. Shortcut expansion respects **Disable Expansion in Fullscreen**.

## Installed applications and Windows behavior

The installed catalog is loaded lazily on a dedicated STA worker from Windows Start Menu and Shell shortcuts, including packaged app links. Duplicate launch identities are collapsed. Window matching prefers executable identity for Win32 applications and AppUserModelID where Windows exposes it. Some applications hide process paths, proxy packaged windows through another process, or ignore close/new-instance requests; in those cases Windows and the target application determine the final behavior.
