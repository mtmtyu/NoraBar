using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NoraBar.Hud.Launcher;

internal interface ILauncherWindowTracker : IDisposable
{
    event EventHandler? InventoryInvalidated;
    DateTimeOffset GetLastActivated(IntPtr windowHandle);
    string? ForegroundApplicationId { get; }
    void Start();
    void Stop();
}

internal sealed class WindowsLauncherWindowTracker : ILauncherWindowTracker
{
    private const uint EventSystemForeground = 0x0003;
    private const uint EventObjectShow = 0x8002;
    private const uint EventObjectHide = 0x8003;
    private const uint WineventOutOfContext = 0x0000;
    private const uint WineventSkipOwnProcess = 0x0002;
    private const int ObjidWindow = 0;

    private readonly object _syncRoot = new();
    private readonly ConcurrentDictionary<IntPtr, DateTimeOffset> _lastActivated = new();
    private readonly ManualResetEventSlim _callbacksIdle = new(initialState: true);
    private readonly WinEventDelegate _callback;
    private IntPtr _foregroundHook;
    private IntPtr _windowHook;
    private int _activeCallbacks;
    private bool _isDisposed;

    internal WindowsLauncherWindowTracker() => _callback = OnWinEvent;

    public event EventHandler? InventoryInvalidated;
    public string? ForegroundApplicationId { get; private set; }

    public DateTimeOffset GetLastActivated(IntPtr windowHandle) =>
        _lastActivated.TryGetValue(windowHandle, out DateTimeOffset value) ? value : DateTimeOffset.MinValue;

    public void Start()
    {
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            if (_foregroundHook != IntPtr.Zero) return;
            _foregroundHook = SetWinEventHook(
                EventSystemForeground, EventSystemForeground, IntPtr.Zero, _callback,
                0, 0, WineventOutOfContext | WineventSkipOwnProcess);
            _windowHook = SetWinEventHook(
                EventObjectShow, EventObjectHide, IntPtr.Zero, _callback,
                0, 0, WineventOutOfContext | WineventSkipOwnProcess);
            if (_foregroundHook == IntPtr.Zero || _windowHook == IntPtr.Zero)
            {
                StopCore();
                throw new InvalidOperationException("Windows window event hooks could not be registered.");
            }
        }
    }

    public void Stop()
    {
        lock (_syncRoot) StopCore();
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_isDisposed) return;
            _isDisposed = true;
            StopCore();
        }

        _callbacksIdle.Wait();
        _lastActivated.Clear();
        ForegroundApplicationId = null;
        InventoryInvalidated = null;
        _callbacksIdle.Dispose();
    }

    private void StopCore()
    {
        if (_foregroundHook != IntPtr.Zero)
        {
            UnhookWinEvent(_foregroundHook);
            _foregroundHook = IntPtr.Zero;
        }
        if (_windowHook != IntPtr.Zero)
        {
            UnhookWinEvent(_windowHook);
            _windowHook = IntPtr.Zero;
        }
    }

    private void OnWinEvent(
        IntPtr hook,
        uint eventType,
        IntPtr windowHandle,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime)
    {
        if (windowHandle == IntPtr.Zero || (objectId != ObjidWindow && objectId != 0)) return;
        lock (_syncRoot)
        {
            if (_isDisposed || _foregroundHook == IntPtr.Zero) return;
            _activeCallbacks++;
            _callbacksIdle.Reset();
        }

        try
        {
            if (eventType == EventSystemForeground)
            {
                _lastActivated[windowHandle] = DateTimeOffset.UtcNow;
                ForegroundApplicationId = WindowsLauncherPlatform.TryGetWindowApplicationIdentity(windowHandle);
            }
            else if (eventType == EventObjectHide)
            {
                _lastActivated.TryRemove(windowHandle, out _);
            }
            try
            {
                InventoryInvalidated?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception exception)
            {
                Trace.TraceError(exception.ToString());
            }
        }
        finally
        {
            lock (_syncRoot)
            {
                _activeCallbacks--;
                if (_activeCallbacks == 0) _callbacksIdle.Set();
            }
        }
    }

    private delegate void WinEventDelegate(
        IntPtr hook, uint eventType, IntPtr windowHandle, int objectId,
        int childId, uint eventThread, uint eventTime);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(
        uint eventMin, uint eventMax, IntPtr eventHookAssembly,
        WinEventDelegate callback, uint processId, uint threadId, uint flags);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(IntPtr hook);
}
