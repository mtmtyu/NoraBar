using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace NoraBar.Hud.Launcher;

internal sealed class LauncherGlobalHotkeys : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int OpenHotkeyId = 0x4E4C;
    private const int SearchHotkeyId = 0x4E53;
    private readonly HwndSource _source;
    private LauncherShortcut? _openShortcut;
    private LauncherShortcut? _searchShortcut;
    private bool _isDisposed;

    internal LauncherGlobalHotkeys(HwndSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        _source.AddHook(WindowMessageHook);
    }

    internal event EventHandler? OpenRequested;
    internal event EventHandler? SearchRequested;

    internal bool TryReplace(
        LauncherShortcut? openShortcut,
        LauncherShortcut? searchShortcut,
        out string? error)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (!TryParse(openShortcut, out uint openModifiers, out uint openKey, out error)
            || !TryParse(searchShortcut, out uint searchModifiers, out uint searchKey, out error))
        {
            return false;
        }

        LauncherShortcut? previousOpen = _openShortcut;
        LauncherShortcut? previousSearch = _searchShortcut;
        UnregisterAll();
        if (!Register(OpenHotkeyId, openShortcut, openModifiers, openKey)
            || !Register(SearchHotkeyId, searchShortcut, searchModifiers, searchKey))
        {
            UnregisterAll();
            TryRegisterPrevious(previousOpen, previousSearch);
            error = "The shortcut is already registered by another application.";
            return false;
        }

        _openShortcut = openShortcut;
        _searchShortcut = searchShortcut;
        error = null;
        return true;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        UnregisterAll();
        _source.RemoveHook(WindowMessageHook);
    }

    private IntPtr WindowMessageHook(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != WmHotkey || _isDisposed) return IntPtr.Zero;
        int id = wParam.ToInt32();
        if (id == OpenHotkeyId) OpenRequested?.Invoke(this, EventArgs.Empty);
        else if (id == SearchHotkeyId) SearchRequested?.Invoke(this, EventArgs.Empty);
        handled = id is OpenHotkeyId or SearchHotkeyId;
        return IntPtr.Zero;
    }

    private void TryRegisterPrevious(LauncherShortcut? open, LauncherShortcut? search)
    {
        if (TryParse(open, out uint openModifiers, out uint openKey, out _)
            && TryParse(search, out uint searchModifiers, out uint searchKey, out _)
            && Register(OpenHotkeyId, open, openModifiers, openKey)
            && Register(SearchHotkeyId, search, searchModifiers, searchKey))
        {
            _openShortcut = open;
            _searchShortcut = search;
            return;
        }

        UnregisterAll();
    }

    private bool Register(int id, LauncherShortcut? shortcut, uint modifiers, uint key) =>
        shortcut is null || RegisterHotKey(_source.Handle, id, modifiers, key);

    private void UnregisterAll()
    {
        UnregisterHotKey(_source.Handle, OpenHotkeyId);
        UnregisterHotKey(_source.Handle, SearchHotkeyId);
        _openShortcut = null;
        _searchShortcut = null;
    }

    private static bool TryParse(LauncherShortcut? shortcut, out uint modifiers, out uint key, out string? error)
    {
        modifiers = 0;
        key = 0;
        error = null;
        if (shortcut is null) return true;
        foreach (string modifier in shortcut.Modifiers.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            modifiers |= modifier.ToUpperInvariant() switch
            {
                "ALT" => 0x0001,
                "CTRL" or "CONTROL" => 0x0002,
                "SHIFT" => 0x0004,
                "WIN" or "WINDOWS" => 0x0008,
                _ => 0u
            };
        }
        if (modifiers == 0)
        {
            error = "Global shortcuts require at least one modifier.";
            return false;
        }
        if (!Enum.TryParse(shortcut.Key, true, out Key parsedKey) || parsedKey == Key.None)
        {
            error = "The shortcut key is invalid.";
            return false;
        }
        key = checked((uint)KeyInterop.VirtualKeyFromKey(parsedKey));
        return key != 0;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
}
