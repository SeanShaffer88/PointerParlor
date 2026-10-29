using System.Buffers.Binary;
using PointerParlor.Core.Imaging;

namespace PointerParlor.Core.Tests;

public class XcursorReaderTests
{
    /// <summary>Builds an Xcursor file: one comment chunk, then the given images (nominal, size, hotspot, delay, premultiplied BGRA fill).</summary>
    private static byte[] Build(params (int Nominal, int Size, int Hx, int Hy, int Delay, byte[] Pixel)[] images)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        var toc = images.Length + 1;
        w.Write("Xcur"u8);
        w.Write(16u);
        w.Write(0x10000u);
        w.Write((uint)toc);
        var pos = 16 + toc * 12;
        // comment chunk entry first, to check non-image chunks are skipped
        w.Write(0xFFFE0001u); w.Write(1u); w.Write((uint)pos);
        pos += 20 + 2;
        foreach (var img in images)
        {
            w.Write(0xFFFD0002u); w.Write((uint)img.Nominal); w.Write((uint)pos);
            pos += 36 + img.Size * img.Size * 4;
        }
        w.Write(20u); w.Write(0xFFFE0001u); w.Write(1u); w.Write(1u); w.Write(2u); w.Write("hi"u8);
        foreach (var img in images)
        {
            w.Write(36u); w.Write(0xFFFD0002u); w.Write((uint)img.Nominal); w.Write(1u);
            w.Write((uint)img.Size); w.Write((uint)img.Size); w.Write((uint)img.Hx); w.Write((uint)img.Hy); w.Write((uint)img.Delay);
            for (var i = 0; i < img.Size * img.Size; i++)
                w.Write(img.Pixel);
        }
        return ms.ToArray();
    }

    [Fact]
    public void Groups_images_by_nominal_size_and_keeps_animation_order()
    {
        var data = Build(
            (24, 24, 3, 4, 50, [0, 0, 255, 255]),
            (32, 32, 5, 6, 50, [0, 0, 255, 255]),
            (24, 24, 3, 4, 70, [0, 255, 0, 255]));

        var images = XcursorReader.Read(data);

        Assert.Equal([24, 32], images.Keys);
        Assert.Equal(2, images[24].Count);
        Assert.Equal((5, 6), (images[32][0].HotspotX, images[32][0].HotspotY));
        Assert.Equal([50, 70], images[24].Select(i => i.DelayMs));
        Assert.Equal(new byte[] { 0, 255, 0, 255 }, images[24][1].Bgra[..4]);
    }

    [Fact]
    public void Converts_premultiplied_pixels_to_straight_alpha()
    {
        var img = XcursorReader.Read(Build((24, 1, 0, 0, 0, [64, 32, 0, 128])))[24][0];
        Assert.Equal(new byte[] { 127, 63, 0, 128 }, img.Bgra);
    }

    [Fact]
    public void Rejects_other_files_and_truncated_images()
    {
        Assert.Throws<CursorFormatException>(() => XcursorReader.Read(Fixtures.Cur()));
        var data = Build((24, 24, 0, 0, 0, [1, 1, 1, 255]));
        Assert.Throws<CursorFormatException>(() => XcursorReader.Read(data.AsSpan(0, data.Length - 100)));
        var badSize = Build((24, 2, 0, 0, 0, [1, 1, 1, 255]));
        BinaryPrimitives.WriteUInt32LittleEndian(badSize.AsSpan(16 + 2 * 12 + 22 + 16), 5000);
        Assert.Throws<CursorFormatException>(() => XcursorReader.Read(badSize));
    }
}
