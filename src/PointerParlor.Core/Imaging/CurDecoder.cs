using System.Buffers.Binary;

namespace PointerParlor.Core.Imaging;

/// <summary>A decoded .cur (or .ico) file: one image per size it carries.</summary>
public sealed record CursorFile(IReadOnlyList<CursorImage> Images)
{
    /// <summary>The smallest image at least <paramref name="size"/> pixels wide, else the largest.</summary>
    public CursorImage BestFor(int size)
    {
        var ordered = Images.OrderBy(i => i.Width).ThenByDescending(i => i.BitDepth).ToList();
        return ordered.FirstOrDefault(i => i.Width >= size) ?? ordered[^1];
    }

    public int LargestSize => Images.Max(i => i.Width);
}

/// <summary>
/// Reads the ICONDIR container used by .cur and .ico files. Entries may hold PNG data or a DIB
/// (XOR color bitmap followed by a 1-bit AND mask, rows bottom-up).
/// </summary>
public static class CurDecoder
{
    private const int MaxDimension = 1024;

    public static CursorFile Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length < 6)
            throw new CursorFormatException("The file is too short to be a cursor.");
        var reserved = BinaryPrimitives.ReadUInt16LittleEndian(data);
        var type = BinaryPrimitives.ReadUInt16LittleEndian(data[2..]);
        var count = BinaryPrimitives.ReadUInt16LittleEndian(data[4..]);
        if (reserved != 0 || type is not (1 or 2))
            throw new CursorFormatException("The file is not a .cur or .ico file.");
        if (count == 0)
            throw new CursorFormatException("The cursor file has no images.");
        if (data.Length < 6 + count * 16)
            throw new CursorFormatException("The cursor file's image directory is cut short.");

        var images = new List<CursorImage>(count);
        for (var i = 0; i < count; i++)
        {
            var e = data.Slice(6 + i * 16, 16);
            // In a .cur these two fields are the hotspot; in an .ico they are planes and bit count.
            var hotX = type == 2 ? BinaryPrimitives.ReadUInt16LittleEndian(e[4..]) : 0;
            var hotY = type == 2 ? BinaryPrimitives.ReadUInt16LittleEndian(e[6..]) : 0;
            var size = BinaryPrimitives.ReadUInt32LittleEndian(e[8..]);
            var offset = BinaryPrimitives.ReadUInt32LittleEndian(e[12..]);
            if (offset >= data.Length || size == 0 || size > data.Length - offset)
                throw new CursorFormatException($"Image {i + 1} points outside the file.");
            images.Add(DecodeEntry(data.Slice((int)offset, (int)size), hotX, hotY, i));
        }
        return new CursorFile(images);
    }

    private static CursorImage DecodeEntry(ReadOnlySpan<byte> img, int hotX, int hotY, int index)
    {
        if (PngWriter.IsPng(img))
        {
            if (!PngWriter.TryReadSize(img, out var pw, out var ph) || pw > MaxDimension || ph > MaxDimension)
                throw new CursorFormatException($"Image {index + 1} has an unreadable PNG header.");
            return CursorImage.FromPng(pw, ph, hotX, hotY, img.ToArray());
        }
        return DecodeDib(img, hotX, hotY, index);
    }

    private static CursorImage DecodeDib(ReadOnlySpan<byte> img, int hotX, int hotY, int index)
    {
        if (img.Length < 40)
            throw new CursorFormatException($"Image {index + 1} is too short.");
        var headerSize = BinaryPrimitives.ReadInt32LittleEndian(img);
        var width = BinaryPrimitives.ReadInt32LittleEndian(img[4..]);
        var height = Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(img[8..])) / 2; // XOR + AND stacked
        var bpp = BinaryPrimitives.ReadUInt16LittleEndian(img[14..]);
        var compression = BinaryPrimitives.ReadUInt32LittleEndian(img[16..]);
        var colorsUsed = BinaryPrimitives.ReadUInt32LittleEndian(img[32..]);

        if (headerSize < 40 || headerSize > img.Length)
            throw new CursorFormatException($"Image {index + 1} has an unknown bitmap header.");
        if (width <= 0 || height <= 0 || width > MaxDimension || height > MaxDimension)
            throw new CursorFormatException($"Image {index + 1} has an invalid size.");
        if (bpp is not (1 or 4 or 8 or 24 or 32))
            throw new CursorFormatException($"Image {index + 1} uses an unsupported color depth ({bpp}-bit).");
        if (compression is not (0 or 3))
            throw new CursorFormatException($"Image {index + 1} uses compressed bitmap data, which is not supported.");

        var pos = headerSize;
        if (compression == 3 && headerSize == 40)
            pos += 12; // BI_BITFIELDS masks follow a plain BITMAPINFOHEADER

        var paletteCount = bpp <= 8 ? (colorsUsed == 0 || colorsUsed > (1u << bpp) ? 1 << bpp : (int)colorsUsed) : 0;
        var xorStride = (width * bpp + 31) / 32 * 4;
        var andStride = (width + 31) / 32 * 4;
        if (img.Length < pos + paletteCount * 4 + xorStride * height)
            throw new CursorFormatException($"Image {index + 1}'s pixel data is cut short.");
        var palette = img.Slice(pos, paletteCount * 4);
        pos += paletteCount * 4;
        var xor = img.Slice(pos, xorStride * height);
        pos += xorStride * height;
        var andAvailable = img.Length >= pos + andStride * height;
        var and = andAvailable ? img.Slice(pos, andStride * height) : ReadOnlySpan<byte>.Empty;

        var bgra = new byte[width * height * 4];
        var useAlphaChannel = false;
        if (bpp == 32)
        {
            for (var i = 3; i < xor.Length; i += 4)
            {
                if (xor[i] != 0) { useAlphaChannel = true; break; }
            }
        }

        var inverted = false;
        for (var y = 0; y < height; y++)
        {
            var srcRow = height - 1 - y; // bottom-up
            var xRow = xor.Slice(srcRow * xorStride, xorStride);
            for (var x = 0; x < width; x++)
            {
                byte b, g, r, a = 255;
                switch (bpp)
                {
                    case 32:
                        b = xRow[x * 4]; g = xRow[x * 4 + 1]; r = xRow[x * 4 + 2];
                        if (useAlphaChannel) a = xRow[x * 4 + 3];
                        break;
                    case 24:
                        b = xRow[x * 3]; g = xRow[x * 3 + 1]; r = xRow[x * 3 + 2];
                        break;
                    default:
                        var idx = PaletteIndex(xRow, x, bpp);
                        if (idx * 4 + 2 < palette.Length)
                        {
                            b = palette[idx * 4]; g = palette[idx * 4 + 1]; r = palette[idx * 4 + 2];
                        }
                        else
                        {
                            b = g = r = 0;
                        }
                        break;
                }

                if (!useAlphaChannel && andAvailable)
                {
                    var maskBit = (and[srcRow * andStride + x / 8] >> (7 - x % 8)) & 1;
                    if (maskBit == 1)
                    {
                        if ((r | g | b) == 0)
                        {
                            a = 0;
                        }
                        else
                        {
                            // Screen-inverting pixel: shown as translucent black in previews.
                            inverted = true;
                            r = g = b = 0;
                            a = 200;
                        }
                    }
                }

                var o = (y * width + x) * 4;
                bgra[o] = b; bgra[o + 1] = g; bgra[o + 2] = r; bgra[o + 3] = a;
            }
        }
        return CursorImage.FromBgra(width, height, hotX, hotY, bpp, bgra, inverted);
    }

    private static int PaletteIndex(ReadOnlySpan<byte> row, int x, int bpp) => bpp switch
    {
        8 => row[x],
        4 => (row[x / 2] >> (x % 2 == 0 ? 4 : 0)) & 0x0F,
        _ => (row[x / 8] >> (7 - x % 8)) & 1,
    };
}
