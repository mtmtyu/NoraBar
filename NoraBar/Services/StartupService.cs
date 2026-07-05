using System;
using System.IO;
using System.Reflection;

namespace NoraBar.Services
{
    public static class StartupService
    {
        private static readonly string StartupFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        private static readonly string ShortcutPath = Path.Combine(StartupFolderPath, "NoraBar.lnk");
        private static readonly string AppPath = System.Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;

        public static bool IsStartupEnabled()
        {
            return File.Exists(ShortcutPath);
        }

        public static void SetStartup(bool enable)
        {
            try
            {
                if (enable)
                {
                    if (File.Exists(ShortcutPath)) return;

                    // Windows Script Host via dynamic to create shortcut without adding COM reference
                    Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                    if (shellType != null)
                    {
                        dynamic shell = Activator.CreateInstance(shellType)!;
                        dynamic shortcut = shell.CreateShortcut(ShortcutPath);
                        shortcut.TargetPath = AppPath;
                        shortcut.WorkingDirectory = Path.GetDirectoryName(AppPath);
                        shortcut.Arguments = "--startup"; // startup mode parameter
                        shortcut.Description = "NoraBar Auto Startup";
                        shortcut.Save();
                    }
                }
                else
                {
                    if (File.Exists(ShortcutPath))
                    {
                        File.Delete(ShortcutPath);
                    }
                }
            }
            catch (Exception)
            {
                // Fail silently
            }
        }
    }
}
