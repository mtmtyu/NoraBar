using System.IO;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NoraBar.Hud.Launcher;

internal sealed class LauncherIconCache
{
    private const int MaximumEntries = 128;
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, ImageSource?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<string> _insertionOrder = new();

    internal Task<ImageSource?> GetAsync(LauncherItem item, CancellationToken cancellationToken)
    {
        string key = item.CustomIconPath ?? item.Target;
        lock (_syncRoot)
        {
            if (_cache.TryGetValue(key, out ImageSource? cached)) return Task.FromResult(cached);
        }
        return Task.Run(() => LoadAndCache(key, item.CustomIconPath is not null, cancellationToken), cancellationToken);
    }

    private ImageSource? LoadAndCache(string path, bool isCustomImage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ImageSource? image;
        try
        {
            image = isCustomImage ? LoadCustomImage(path) : LoadShellIcon(path);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            image = null;
        }
        image?.Freeze();
        lock (_syncRoot)
        {
            if (_cache.TryGetValue(path, out ImageSource? existing)) return existing;
            while (_cache.Count >= MaximumEntries && _insertionOrder.TryDequeue(out string? oldest)) _cache.Remove(oldest);
            _cache[path] = image;
            _insertionOrder.Enqueue(path);
        }
        return image;
    }

    private static ImageSource? LoadCustomImage(string path)
    {
        if (!File.Exists(path)) return null;
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.DecodePixelWidth = 64;
        bitmap.EndInit();
        return bitmap;
    }

    private static ImageSource? LoadShellIcon(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return null;
        using System.Drawing.Icon? icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
        return icon is null ? null : Imaging.CreateBitmapSourceFromHIcon(
            icon.Handle, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(48, 48));
    }
}
