using System.Buffers.Binary;
using System.IO.Compression;

namespace PointerParlor.Core.Imaging;

/// <summary>Minimal PNG encoder (8-bit RGBA, no filtering) so previews need no imaging library.</summary>
public static class PngWriter
{
    private static readonly byte[] Signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly uint[] CrcTable = BuildCrcTable();

    public static byte[] EncodeBgra(int width, int height, ReadOnlySpan<byte> bgra)
    {
        using var raw = new MemoryStream(height * (width * 4 + 1));
        for (var y = 0; y < height; y++)
        {
            raw.WriteByte(0); // filter: none
            var row = bgra.Slice(y * width * 4, width * 4);
            for (var x = 0; x < width; x++)
            {
                var p = x * 4;
                raw.WriteByte(row[p + 2]);
                raw.WriteByte(row[p + 1]);
                raw.WriteByte(row[p]);
                raw.WriteByte(row[p + 3]);
            }
        }

        byte[] compressed;
        using (var zout = new MemoryStream())
        {
            using (var z = new ZLibStream(zout, CompressionLevel.Fastest, leaveOpen: true))
                raw.WriteTo(z);
            compressed = zout.ToArray();
        }

        using var png = new MemoryStream();
        png.Write(Signature);
        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr[4..], height);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 6;  // color type: RGBA
        ihdr[10] = 0; // compression
        ihdr[11] = 0; // filter
        ihdr[12] = 0; // interlace
        WriteChunk(png, "IHDR", ihdr);
        WriteChunk(png, "IDAT", compressed);
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    /// <summary>Reads width and height from a PNG's IHDR chunk.</summary>
    public static bool TryReadSize(ReadOnlySpan<byte> png, out int width, out int height)
    {
        width = height = 0;
        if (png.Length < 24 || !png[..8].SequenceEqual(Signature))
            return false;
        width = BinaryPrimitives.ReadInt32BigEndian(png[16..]);
        height = BinaryPrimitives.ReadInt32BigEndian(png[20..]);
        return width > 0 && height > 0;
    }

    public static bool IsPng(ReadOnlySpan<byte> data) => data.Length >= 8 && data[..8].SequenceEqual(Signature);

    private static void WriteChunk(Stream s, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buf, data.Length);
        s.Write(buf);
        Span<byte> typeBytes = [(byte)type[0], (byte)type[1], (byte)type[2], (byte)type[3]];
        s.Write(typeBytes);
        s.Write(data);
        var crc = Crc(Crc(0xFFFFFFFFu, typeBytes), data) ^ 0xFFFFFFFFu;
        BinaryPrimitives.WriteUInt32BigEndian(buf, crc);
        s.Write(buf);
    }

    private static uint Crc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var b in data)
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }
}
