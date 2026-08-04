using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Text;

namespace NoraBar.Hud.Launcher;

internal static class LauncherShortcutResolver
{
    internal static LauncherItem? TryCreate(string shortcutPath)
    {
        string extension = Path.GetExtension(shortcutPath);
        if (extension.Equals(".url", StringComparison.OrdinalIgnoreCase))
        {
            string? url = File.ReadLines(shortcutPath)
                .FirstOrDefault(line => line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))?[4..].Trim();
            return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                ? Create(Path.GetFileNameWithoutExtension(shortcutPath), LauncherItemKind.Url, uri.AbsoluteUri)
                : null;
        }

        if (!extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase)) return null;
        string? target = null;
        string? arguments = null;
        string? applicationUserModelId = null;
        IShellLinkW? shellLink = null;
        try
        {
            shellLink = (IShellLinkW)(object)new ShellLink();
            ((IPersistFile)shellLink).Load(shortcutPath, 0);
            var path = new StringBuilder(32768);
            shellLink.GetPath(path, path.Capacity, out _, 0);
            target = path.Length > 0 ? Environment.ExpandEnvironmentVariables(path.ToString()) : null;
            var args = new StringBuilder(32768);
            shellLink.GetArguments(args, args.Capacity);
            arguments = args.Length > 0 ? args.ToString() : null;
            if (shellLink is IPropertyStore propertyStore)
            {
                PropertyKey key = PropertyKey.ApplicationUserModelId;
                propertyStore.GetValue(ref key, out PropertyVariant value);
                try { applicationUserModelId = value.GetString(); }
                finally { PropVariantClear(ref value); }
            }
        }
        catch (COMException) { }
        finally
        {
            if (shellLink is not null) Marshal.FinalReleaseComObject(shellLink);
        }

        string displayName = Path.GetFileNameWithoutExtension(shortcutPath);
        if (!string.IsNullOrWhiteSpace(target))
        {
            LauncherItemKind kind = Path.GetExtension(target).Equals(".exe", StringComparison.OrdinalIgnoreCase)
                ? LauncherItemKind.Win32Application
                : LauncherItemKind.File;
            return Create(displayName, kind, target, arguments);
        }

        string packagedTarget = string.IsNullOrWhiteSpace(applicationUserModelId)
            ? Path.GetFullPath(shortcutPath)
            : applicationUserModelId;
        return Create(displayName, LauncherItemKind.PackagedApplication, packagedTarget);
    }

    private static LauncherItem Create(
        string displayName,
        LauncherItemKind kind,
        string target,
        string? arguments = null)
    {
        string identity = $"{kind}\0{target}\0{arguments}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity.ToUpperInvariant()));
        return new LauncherItem(
            $"app-{Convert.ToHexString(hash.AsSpan(0, 12)).ToLowerInvariant()}",
            displayName,
            kind,
            target,
            arguments);
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private sealed class ShellLink { }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out] StringBuilder file, int maximumPath, out Win32FindData data, uint flags);
        void GetIDList(out IntPtr itemIdList);
        void SetIDList(IntPtr itemIdList);
        void GetDescription([Out] StringBuilder description, int maximumName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string description);
        void GetWorkingDirectory([Out] StringBuilder directory, int maximumPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        void GetArguments([Out] StringBuilder arguments, int maximumPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int showCommand);
        void SetShowCmd(int showCommand);
        void GetIconLocation([Out] StringBuilder iconPath, int maximumPath, out int iconIndex);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(IntPtr windowHandle, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    private interface IPropertyStore
    {
        void GetCount(out uint propertyCount);
        void GetAt(uint propertyIndex, out PropertyKey key);
        void GetValue(ref PropertyKey key, out PropertyVariant value);
        void SetValue(ref PropertyKey key, ref PropertyVariant value);
        void Commit();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey
    {
        private Guid formatId;
        private uint propertyId;
        internal static PropertyKey ApplicationUserModelId => new()
        {
            formatId = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
            propertyId = 5
        };
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PropertyVariant
    {
        [FieldOffset(0)] private ushort valueType;
        [FieldOffset(8)] private IntPtr pointer;
        internal string? GetString() => valueType == 31 && pointer != IntPtr.Zero
            ? Marshal.PtrToStringUni(pointer)
            : null;
    }

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropertyVariant value);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Win32FindData
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint Reserved0;
        public uint Reserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string FileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)] public string AlternateFileName;
    }
}
