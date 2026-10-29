namespace PointerParlor.Core.Imaging;

/// <summary>Thrown when a file is not a readable .cur, .ico or .ani file.</summary>
public sealed class CursorFormatException(string message) : Exception(message);

/// <summary>
/// One image inside a cursor file. Holds either decoded pixels (straight-alpha BGRA, top-down)
/// or the PNG bytes the file embedded; <see cref="ToPng"/> gives PNG either way.
/// </summary>
public sealed class CursorImage
{
    private readonly byte[]? _bgra;
    private readonly byte[]? _png;

    private CursorImage(int width, int height, int hotspotX, int hotspotY, int bitDepth, byte[]? bgra, byte[]? png, bool hasInvertedPixels)
    {
        Width = width;
        Height = height;
        HotspotX = hotspotX;
        HotspotY = hotspotY;
        BitDepth = bitDepth;
        _bgra = bgra;
        _png = png;
        HasInvertedPixels = hasInvertedPixels;
    }

    public int Width { get; }
    public int Height { get; }
    public int HotspotX { get; }
    public int HotspotY { get; }
    public int BitDepth { get; }

    /// <summary>
    /// True when the image uses screen-inverting pixels (AND mask set over a non-black color).
    /// Previews show those pixels as dark, which is an approximation.
    /// </summary>
    public bool HasInvertedPixels { get; }

    public bool IsPng => _png is not null;

    public static CursorImage FromBgra(int width, int height, int hotspotX, int hotspotY, int bitDepth, byte[] bgra, bool hasInvertedPixels = false)
    {
        if (bgra.Length != width * height * 4)
            throw new ArgumentException("Pixel buffer size does not match the image size.", nameof(bgra));
        return new CursorImage(width, height, hotspotX, hotspotY, bitDepth, bgra, null, hasInvertedPixels);
    }

    public static CursorImage FromPng(int width, int height, int hotspotX, int hotspotY, byte[] png) =>
        new(width, height, hotspotX, hotspotY, 32, null, png, false);

    public byte[] ToPng() => _png ?? PngWriter.EncodeBgra(Width, Height, _bgra!);

    /// <summary>Decoded pixels, or null when the image is stored as PNG.</summary>
    public ReadOnlySpan<byte> Bgra => _bgra;
}
