using System.Buffers.Binary;

namespace PointerParlor.Core.Imaging;

/// <summary>One image in an Xcursor file: straight-alpha BGRA pixels, hotspot, and animation delay.</summary>
public sealed record XcursorImage(int NominalSize, int Width, int Height, int HotspotX, int HotspotY, int DelayMs, byte[] Bgra);

/// <summary>
/// Reads Xcursor files, the cursor format of Linux desktop themes: a table of contents pointing
/// to image chunks, each tagged with a nominal size. Several images of one size form an animation.
/// </summary>
public static class XcursorReader
{
    private const uint ImageType = 0xFFFD0002;
    private const int MaxDimension = 1024;

    public static bool IsXcursor(ReadOnlySpan<byte> data) => data.Length >= 4 && data[..4].SequenceEqual("Xcur"u8);

    /// <summary>Every image in file order, grouped by nominal size (ascending).</summary>
    public static IReadOnlyDictionary<int, IReadOnlyList<XcursorImage>> Read(ReadOnlySpan<byte> data)
    {
        if (!IsXcursor(data) || data.Length < 16)
            throw new CursorFormatException("The file is not an Xcursor file.");
        var headerSize = BinaryPrimitives.ReadUInt32LittleEndian(data[4..]);
        var count = BinaryPrimitives.ReadUInt32LittleEndian(data[12..]);
        if (headerSize < 16 || count > 4096 || headerSize + count * 12L > data.Length)
            throw new CursorFormatException("The Xcursor table of contents is cut short.");

        var bySize = new SortedDictionary<int, List<XcursorImage>>();
        for (var i = 0; i < count; i++)
        {
            var toc = data.Slice((int)headerSize + i * 12, 12);
            if (BinaryPrimitives.ReadUInt32LittleEndian(toc) != ImageType)
                continue; // comments and other chunks
            var position = BinaryPrimitives.ReadUInt32LittleEndian(toc[8..]);
            var image = ReadImage(data, position);
            if (!bySize.TryGetValue(image.NominalSize, out var list))
                bySize[image.NominalSize] = list = [];
            list.Add(image);
        }
        if (bySize.Count == 0)
            throw new CursorFormatException("The Xcursor file has no images.");
        return bySize.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<XcursorImage>)kv.Value);
    }

    public static IReadOnlyDictionary<int, IReadOnlyList<XcursorImage>> ReadFile(string path) => Read(File.ReadAllBytes(path));

    private static XcursorImage ReadImage(ReadOnlySpan<byte> data, uint position)
    {
        if (position > data.Length - 36L)
            throw new CursorFormatException("An Xcursor image points outside the file.");
        var chunk = data[(int)position..];
        var header = BinaryPrimitives.ReadUInt32LittleEndian(chunk);
        var nominal = (int)BinaryPrimitives.ReadUInt32LittleEndian(chunk[8..]);
        var width = (int)BinaryPrimitives.ReadUInt32LittleEndian(chunk[16..]);
        var height = (int)BinaryPrimitives.ReadUInt32LittleEndian(chunk[20..]);
        var hotX = (int)BinaryPrimitives.ReadUInt32LittleEndian(chunk[24..]);
        var hotY = (int)BinaryPrimitives.ReadUInt32LittleEndian(chunk[28..]);
        var delay = (int)BinaryPrimitives.ReadUInt32LittleEndian(chunk[32..]);
        if (width is <= 0 or > MaxDimension || height is <= 0 or > MaxDimension || header < 36)
            throw new CursorFormatException("An Xcursor image has an invalid size.");
        if (chunk.Length < header + (long)width * height * 4)
            throw new CursorFormatException("An Xcursor image's pixels are cut short.");

        // Pixels are premultiplied ARGB words, little-endian: bytes B, G, R, A. Convert to straight alpha.
        var pixels = chunk.Slice((int)header, width * height * 4).ToArray();
        for (var p = 0; p < pixels.Length; p += 4)
        {
            var a = pixels[p + 3];
            if (a is 0 or 255)
                continue;
            pixels[p] = (byte)Math.Min(255, pixels[p] * 255 / a);
            pixels[p + 1] = (byte)Math.Min(255, pixels[p + 1] * 255 / a);
            pixels[p + 2] = (byte)Math.Min(255, pixels[p + 2] * 255 / a);
        }
        return new XcursorImage(nominal, width, height, Math.Clamp(hotX, 0, width - 1), Math.Clamp(hotY, 0, height - 1), delay, pixels);
    }
}
