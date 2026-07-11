using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace NoraBar.Services
{
    public static class FullscreenDetector
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        /// <summary>
        /// Checks if a fullscreen application is currently active on the same screen as the given window.
        /// </summary>
        public static bool IsFullscreenAppActive(Window window)
        {
            var foregroundWindow = GetForegroundWindow();
            
            // Exclude desktop and no-window states
            if (foregroundWindow == IntPtr.Zero || foregroundWindow == GetDesktopWindow() || foregroundWindow == GetShellWindow())
            {
                return false;
            }

            // Get the screen where the current window is located
            var windowInterop = new WindowInteropHelper(window);
            // Handle might not be created yet in some very early stages, but UpdateView happens later.
            if (windowInterop.Handle == IntPtr.Zero)
            {
                return false;
            }
            var currentScreen = System.Windows.Forms.Screen.FromHandle(windowInterop.Handle);

            // Get the bounds of the foreground window
            if (GetWindowRect(foregroundWindow, out var rect))
            {
                // Verify if the foreground window covers the entire current screen
                if (rect.Left <= currentScreen.Bounds.Left &&
                    rect.Top <= currentScreen.Bounds.Top &&
                    rect.Right >= currentScreen.Bounds.Right &&
                    rect.Bottom >= currentScreen.Bounds.Bottom)
                {
                    // Confirm the foreground window is actually located on the same screen
                    var foregroundScreen = System.Windows.Forms.Screen.FromHandle(foregroundWindow);
                    if (foregroundScreen.DeviceName == currentScreen.DeviceName)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
