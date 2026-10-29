using System.Buffers.Binary;
using System.Text;

namespace PointerParlor.Core.Imaging;

/// <summary>A cursor image to write: straight-alpha BGRA pixels, top-down.</summary>
public sealed record CursorFrameSource(int Width, int Height, int HotspotX, int HotspotY, byte[] Bgra);

/// <summary>
/// Writes .cur files (32-bit DIB with an AND mask, one entry per size) and .ani files.
/// Used to build the bundled sets and to make test files.
/// </summary>
public static class CursorWriter
{
    public static byte[] WriteCur(IReadOnlyList<CursorFrameSource> images)
    {
        if (images.Count == 0)
            throw new ArgumentException("A cursor needs at least one image.", nameof(images));

        var bodies = images.Select(EncodeDib).ToList();
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((ushort)0);
        w.Write((ushort)2); // cursor
        w.Write((ushort)images.Count);
        var offset = 6 + 16 * images.Count;
        for (var i = 0; i < images.Count; i++)
        {
            var img = images[i];
            w.Write((byte)(img.Width >= 256 ? 0 : img.Width));
            w.Write((byte)(img.Height >= 256 ? 0 : img.Height));
            w.Write((byte)0);
            w.Write((byte)0);
            w.Write((ushort)img.HotspotX);
            w.Write((ushort)img.HotspotY);
            w.Write(bodies[i].Length);
            w.Write(offset);
            offset += bodies[i].Length;
        }
        foreach (var body in bodies)
            w.Write(body);
        return ms.ToArray();
    }

    /// <param name="frames">Each frame as a complete .cur file.</param>
    /// <param name="jiffies">Display time per frame in 1/60 s.</param>
    public static byte[] WriteAni(IReadOnlyList<byte[]> frames, IReadOnlyList<int> jiffies, string? title = null, string? artist = null)
    {
        if (frames.Count == 0 || jiffies.Count != frames.Count)
            throw new ArgumentException("An animated cursor needs one display time per frame.", nameof(jiffies));

        using var body = new MemoryStream();
        body.Write("ACON"u8);

        if (title is not null || artist is not null)
        {
            using var info = new MemoryStream();
            info.Write("INFO"u8);
            if (title is not null) WriteChunk(info, "INAM", Encoding.Latin1.GetBytes(title + "\0"));
            if (artist is not null) WriteChunk(info, "IART", Encoding.Latin1.GetBytes(artist + "\0"));
            WriteChunk(body, "LIST", info.ToArray());
        }

        var anih = new byte[36];
        BinaryPrimitives.WriteUInt32LittleEndian(anih, 36);
        BinaryPrimitives.WriteUInt32LittleEndian(anih.AsSpan(4), (uint)frames.Count);
        BinaryPrimitives.WriteUInt32LittleEndian(anih.AsSpan(8), (uint)frames.Count);
        BinaryPrimitives.WriteUInt32LittleEndian(anih.AsSpan(28), (uint)jiffies[0]);
        BinaryPrimitives.WriteUInt32LittleEndian(anih.AsSpan(32), 1); // AF_ICON: frames are cursor files
        WriteChunk(body, "anih", anih);

        var rate = new byte[4 * jiffies.Count];
        for (var i = 0; i < jiffies.Count; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(rate.AsSpan(i * 4), (uint)jiffies[i]);
        WriteChunk(body, "rate", rate);

        using (var fram = new MemoryStream())
        {
            fram.Write("fram"u8);
            foreach (var frame in frames)
                WriteChunk(fram, "icon", frame);
            WriteChunk(body, "LIST", fram.ToArray());
        }

        using var file = new MemoryStream();
        WriteChunk(file, "RIFF", body.ToArray());
        return file.ToArray();
    }

    private static byte[] EncodeDib(CursorFrameSource img)
    {
        if (img.Bgra.Length != img.Width * img.Height * 4)
            throw new ArgumentException("Pixel buffer size does not match the image size.");
        var andStride = (img.Width + 31) / 32 * 4;
        var dib = new byte[40 + img.Width * img.Height * 4 + andStride * img.Height];
        BinaryPrimitives.WriteInt32LittleEndian(dib, 40);
        BinaryPrimitives.WriteInt32LittleEndian(dib.AsSpan(4), img.Width);
        BinaryPrimitives.WriteInt32LittleEndian(dib.AsSpan(8), img.Height * 2);
        BinaryPrimitives.WriteUInt16LittleEndian(dib.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(dib.AsSpan(14), 32);

        var pixels = dib.AsSpan(40);
        var mask = dib.AsSpan(40 + img.Width * img.Height * 4);
        for (var y = 0; y < img.Height; y++)
        {
            var dstRow = img.Height - 1 - y; // bottom-up
            img.Bgra.AsSpan(y * img.Width * 4, img.Width * 4).CopyTo(pixels[(dstRow * img.Width * 4)..]);
            for (var x = 0; x < img.Width; x++)
            {
                if (img.Bgra[(y * img.Width + x) * 4 + 3] == 0)
                    mask[dstRow * andStride + x / 8] |= (byte)(0x80 >> (x % 8));
            }
        }
        return dib;
    }

    private static void WriteChunk(Stream s, string id, byte[] data)
    {
        s.Write(Encoding.ASCII.GetBytes(id));
        Span<byte> size = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(size, (uint)data.Length);
        s.Write(size);
        s.Write(data);
        if (data.Length % 2 == 1)
            s.WriteByte(0);
    }
}
