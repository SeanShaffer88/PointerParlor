using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;
using PointerParlor.Core.Imaging;
using Windows.Storage.Streams;

namespace PointerParlor.Services;

/// <summary>A cursor ready to show: one bitmap per animation step.</summary>
public sealed record CursorPreviewData(IReadOnlyList<(BitmapImage Image, int DurationMs)> Steps, int PixelSize, int LargestSize);

/// <summary>
/// Decodes cursor files off the UI thread and turns their frames into bitmaps. Results are cached
/// per file and size; the cache is small because a page shows at most a few dozen cursors.
/// </summary>
public static class PreviewLoader
{
    private static readonly Dictionary<(string, int), Task<CursorPreviewData?>> Cache = [];

    /// <summary>Loads a preview; must be called on the UI thread. Returns null when the file cannot be read.</summary>
    public static Task<CursorPreviewData?> LoadAsync(string path, int size)
    {
        var key = (path.ToUpperInvariant(), size);
        if (!Cache.TryGetValue(key, out var task))
        {
            if (Cache.Count > 400)
                Cache.Clear();
            task = LoadCoreAsync(path, size);
            Cache[key] = task;
        }
        return task;
    }

    public static void Forget(string path)
    {
        foreach (var key in Cache.Keys.Where(k => k.Item1 == path.ToUpperInvariant()).ToList())
            Cache.Remove(key);
    }

    /// <summary>The largest image size in a cursor file, read off the UI thread; 0 when unreadable.</summary>
    public static Task<int> LargestSizeAsync(string path) => Task.Run(() =>
    {
        try { return CursorReader.ReadFile(path).LargestSize; }
        catch (Exception ex) when (ex is CursorFormatException or IOException or UnauthorizedAccessException) { return 0; }
    });

    private static async Task<CursorPreviewData?> LoadCoreAsync(string path, int size)
    {
        (byte[] Png, int DurationMs)[] steps;
        int pixelSize, largest;
        try
        {
            (steps, pixelSize, largest) = await Task.Run(() =>
            {
                var asset = CursorReader.ReadFile(path);
                var frames = asset.Frames.Select(f => f.BestFor(size)).ToList();
                var pngs = frames.Select(f => f.ToPng()).ToList();
                var list = asset.Steps.Select(s => (pngs[s.FrameIndex], s.DurationMs)).ToArray();
                return (list, frames[0].Width, asset.LargestSize);
            });
        }
        catch (Exception ex) when (ex is CursorFormatException or IOException or UnauthorizedAccessException)
        {
            AppLog.Error($"Preview failed for {Path.GetFileName(path)}", ex);
            return null;
        }

        // Steps often reuse frames; build each distinct bitmap once.
        var bitmaps = new Dictionary<byte[], BitmapImage>(ReferenceEqualityComparer.Instance);
        var result = new List<(BitmapImage, int)>(steps.Length);
        foreach (var (png, duration) in steps)
        {
            if (!bitmaps.TryGetValue(png, out var bitmap))
            {
                bitmap = new BitmapImage();
                using var stream = new InMemoryRandomAccessStream();
                await stream.WriteAsync(png.AsBuffer());
                stream.Seek(0);
                await bitmap.SetSourceAsync(stream);
                bitmaps[png] = bitmap;
            }
            result.Add((bitmap, duration));
        }
        return new CursorPreviewData(result, pixelSize, largest);
    }
}
